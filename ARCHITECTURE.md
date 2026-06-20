# ARCHITECTURE — TaxiApp

Generated from the actual codebase on 2026-06-20. Covers backend internal architecture, mobile internal architecture, and how the two interact.

---

## 1. Backend architecture

### 1.1 Layering

Classic 3-project Clean Architecture, enforced by project references (not just convention):

```
TaxiApp.Backend.Core            ← no project references (pure domain: Models, DTO'S, Interfaces, JwtService, Settings)
        ↑
TaxiApp.Backend.Infrastructure  ← references Core only (Data/ApplicationDbContext, Repositories, Helper/SignalR+services, Migrations)
        ↑
TaxiApp.Backend (Api)           ← references Infrastructure (Controllers, Program.cs)
```

Controllers depend only on `Core.Interfaces` (e.g. `IOrderRepository`, `INotificationRepository`) — never on Infrastructure concrete types — with **one exception**: `NotificationsController` also injects `ApplicationDbContext` directly. It's unused dead code rather than an active violation, but it's the one crack in an otherwise consistently enforced boundary.

There is no separate "Service" layer for most features — repositories carry both data access and business logic (e.g. `AuthRepository` does OTP generation, lockout tracking, and JWT issuance, not just persistence). Two named exceptions exist: `OrderService` and `TripRoutingService`, both registered as scoped services in `Program.cs`, sitting above the repositories for cross-cutting order/trip orchestration.

### 1.2 Request pipeline (`Program.cs`)

```
Request
  → CORS ("AllowAll": any origin + credentials)
  → Static files (/images/*)
  → Authentication (JWT Bearer; query-string token only for /notificationHub)
  → Rate Limiter (named per-endpoint policies)
  → Authorization ([Authorize(Roles=...)])
  → Controller action
  → Repository (EF Core LINQ against ApplicationDbContext)
  → SignalR push (where relevant) + JSON response
```

Startup also runs role-seeding (`SuperAdmin`/`Admin`/`Driver`/`Passenger`) and `DbSeeder.SeedAdminAsync` before the middleware pipeline is built.

### 1.3 Data layer

Single `ApplicationDbContext : IdentityDbContext<ApplicationUser>`. All data access is EF Core LINQ — no raw SQL anywhere in the codebase. Soft-delete is modeled per-entity (`IsDeleted` + query filters on `Driver`/`Passenger`) rather than via a global EF interceptor/convention.

### 1.4 Realtime layer

One SignalR hub (`NotificationHub` at `/notificationHub`) is the sole realtime channel for the entire system — notifications, trip status, driver location, chat, and route updates all multiplex through it via named groups:
- `user-{userId}` — every connected client auto-joins their own group.
- `office` — Admins additionally join this shared group (sees all unassigned driver locations).
- `trip-{tripId}` — joined/left explicitly via the `JoinTrip`/`LeaveTrip` hub methods, scoping trip-specific events (location, route, messages) to participants of that trip.

Repositories and services push to these groups directly via `IHubContext<NotificationHub>` — there's no outbox/event-bus indirection; a SignalR push is just another side effect at the end of a repository method (e.g. `NotificationRepository.SendNotificationAsync`, `MessageRepository.SendMessageAsync`, `DriverTrackingRepository.BroadcastLocation`, `TripRoutingService.RecalculateTripAsync`).

### 1.5 Background processing

Three `IHostedService` implementations run continuously alongside the web host:
- `TripOfferBackgroundService` — sweeps unanswered trip offers (timeout → re-offer/escalate), detects delayed trips and fires `DelayWarning` notifications.
- `RefreshTokenCleanupService` — purges expired/revoked refresh tokens.
- `DatabaseCleanupService` — general data-retention purge (e.g. old `DriverLocation` rows).

### 1.6 Dispatch design

The core domain decision in this system is **how a Driver gets matched to an Order**, implemented in `DriverAssignmentRepository` (the largest single file in the codebase): nearby available drivers are scored and offered the trip one at a time (or in a shared/batched mode depending on `OrderPriority`/`SystemSettings.SystemMode`); an unanswered offer times out via the background sweep and moves to the next candidate; an Admin can short-circuit this via `DriverAssignmentManualController`/`AdminAssignmentRepository` regardless of mode. `SystemSettings.SystemMode` (Auto/Manual) is a global on/off switch for the automatic half of this engine — when Manual, every order requires Admin action. `AdminAssignmentRepository.GetAssignableDriversAsync` (added 2026-06-20 for the Admin mobile app's manual-assignment picker) is a read-only sibling to this engine, not part of it — it just lists non-deleted, non-rejected drivers by name for the Admin UI to pick from; it doesn't share any scoring logic with the actual dispatch engine.

---

## 2. Mobile architecture

### 2.1 Pattern

Feature-based "clean architecture lite": each feature folder under `lib/features/` is internally layered `data → domain → presentation`, independent of the other features. There's no dedicated shared-domain package, but the `admin/` feature (built 2026-06-20) established the actual answer to the "what happens when two features need the same backend concept" question flagged here previously: it imports `OrderStatus`/`OrderPriority`/`VehicleSize`/`OrderRating`/`GeoPoint`/`TripStatus` directly from `orders/domain/entities/` and `DriverStatus` from `driver/domain/entities/`, rather than redefining its own copies. Feature-specific entities (e.g. `DriverApprovalDetail`, `Complaint`) still live in `admin/` itself — only the enums/value-types that mirror a backend type already owned by another feature get reused cross-feature.

```
presentation (screens/widgets)
      ↓ reads/calls
providers (Riverpod Notifier/FamilyNotifier — sealed state)
      ↓ calls
domain (repository interface, entities)
      ↓ implemented by
data (repository impl → *Api class → Dio → backend)
```

`lib/core/` holds everything cross-cutting: network client + interceptors, the SignalR wrapper, routing, secure storage, theming, shared widgets/utils. Nothing in `core/` depends on any `features/*` code — dependency direction is strictly inward.

### 2.2 State management

Riverpod 2.x, `Notifier`/`FamilyNotifier` (no codegen/`riverpod_generator`). Each feature has its own sealed state type (`AuthState`, `OrderDetailState`, `OrdersListState`, `NotificationsListState`, `ProfileState`); screens are `ConsumerWidget`s that `switch` over the state to render loading/error/data variants. Providers are split: `*_providers.dart` wires API client → repository (DI), `*_controller.dart` holds the actual `Notifier` business logic that screens call into. UI never talks to a repository or API class directly.

### 2.3 Networking

`dio_client.dart` builds two `Dio` instances: the main client (with `AuthInterceptor` + `LoggingInterceptor`) and a second, bare `refreshDio` used only inside the auth interceptor's own token-refresh call — this avoids the refresh request recursing through the same interceptor that triggered it.

`AuthInterceptor` attaches `Bearer <token>` on every outgoing request and, on a 401, performs a single refresh, coalescing concurrent 401s onto one in-flight refresh future (`_refreshFuture ??= ...`) so N simultaneous failing requests don't trigger N refresh calls. On unrecoverable refresh failure it raises a session-expired signal that `AuthController` listens to and reacts to by clearing the session (router then redirects to `/welcome` automatically via the redirect-on-state-change pattern below).

Base URL is configured via `String.fromEnvironment('API_BASE_URL', defaultValue: 'http://10.0.2.2:5033/api')` — not hardcoded, overridable per build via `--dart-define`.

### 2.4 Routing

`go_router`, single `appRouterProvider`. Auth gating lives entirely in the router's `redirect` callback, keyed off the current `AuthState`: unauthenticated users are confined to a public-route allowlist (else bounced to `/welcome`), authenticated users are bounced away from auth-only routes toward their role's home. A custom `_GoRouterRefreshNotifier` bridges Riverpod state changes into go_router's `Listenable`-based `refreshListenable`, so a login/logout event re-evaluates routing immediately without any manual `context.go(...)` call from the screens themselves.

### 2.5 Realtime

`RealtimeService` wraps a single `HubConnection` (signalr_netcore) to `/notificationHub`, built lazily on first `connect()`. The backend's JWT is supplied via `accessTokenFactory` (sent as `?access_token=` on the WS handshake, matching the backend's special-cased `OnMessageReceived` handler). Three of the backend's five hub events are currently consumed (`ReceiveNotification`, `UpdateTripStatus`, `DriverLocationUpdated`) and re-exposed as broadcast `Stream`s, not callbacks — screens subscribe via Riverpod controllers, which cancel their subscriptions in `ref.onDispose`. Reconnection relies entirely on signalr_netcore's built-in `withAutomaticReconnect()`; if the *initial* connect fails, the service treats live updates as a progressive enhancement and silently falls back to whatever REST data the screen already has — no error surfaces to the user.

### 2.6 The "one screen" design choice

`OrderDetailScreen` deliberately merges what the product brief listed as two separate screens — "order details" and "trip tracking" — into one, switching its body based on `order.hasDriver` (searching spinner → live map + driver card) and `order.status` (adds a cancel button while cancellable, a rating card once completed). This mirrors how Uber/Careem-style apps work in practice: there's no real product moment where "an order" and "that same order, now with a driver" are different screens a user navigates between.

---

## 3. Data flow: a full order lifecycle, end to end

1. **Passenger app**: `HomeScreen` → taps pickup/dropoff on `LocationPickerMap` (flutter_map) → `POST /Orders/CreateOrder` → backend creates `Order` (status `Pending`/`SearchingDriver`) → `OrderDetailDto` returned, app navigates to `OrderDetailScreen`.
2. **Backend dispatch**: `DriverAssignmentRepository` finds nearby available drivers, offers the trip to one (`TripOfferSentAt`/`LastOfferedDriverId` set on the Order/Trip) → pushes a notification to that driver's `user-{driverId}` group.
3. **Driver accepts** (via the Driver app's Dashboard, or an Admin's manual assignment): `DriverTripsController.AcceptOrder` → `Trip`/`TripOrder` created/linked, status → `Assigned` → `NotificationRepository` pushes `ReceiveNotification` + `UpdateTripStatus` to `user-{passengerId}` and `trip-{tripId}`.
4. **Passenger app**: `OrderDetailScreen` is already subscribed (`JoinTrip`) and its Riverpod controller is listening to the `tripStatusUpdates`/`notifications` streams → UI flips from "searching" to the live-tracking view (`TripTrackingMap` + driver card) reactively, no manual refresh/poll.
5. **Driver moves**: the Driver app's background `SendLocation` hub calls → `DriverTrackingRepository` throttles writes (only persists if moved AND ≥10s elapsed) and always refreshes `Driver.LastLat/LastLng` → broadcasts `DriverLocationUpdated` to `office` (if not in queue) and `trip-{tripId}`.
6. **Passenger app**: `driverLocationUpdates` stream updates the marker on `TripTrackingMap` in near-real-time. No route polyline is drawn — see §"Known gap" below.
7. **Trip completes**: Driver dropoff action (from the Driver app's Active Trip card) → status `Completed` → Passenger can rate the driver (display-only on mobile today — `PassengerTripsController.rate-driver` exists but has no Flutter submission UI yet) and chat with the driver per-order (`MessagesController` + the shared `chat/` feature, built for both Passenger and Driver).

### Known gap in this flow
`TripRoutingService.RecalculateTripAsync` pushes `RouteUpdated` (recalculated polyline/per-stop ETA) **only** to the driver's own group (`user-{driverId}`), never to `trip-{tripId}`. The passenger-side `TripTrackingMap` therefore only ever shows the driver's raw lat/lng — never a drawn route or live ETA — by backend design/omission, not a Flutter bug. Fixing it means changing `TripRoutingService`, not the mobile app.

---

## 4. Backend ↔ Flutter interaction summary

| Concern | Backend | Mobile |
|---|---|---|
| Transport | REST (JSON) + 1 SignalR hub | `dio` + `signalr_netcore` |
| Auth | JWT Bearer, 8h expiry, refresh-token rotation w/ theft detection | Secure-stored tokens, auto-attach + single coalesced refresh-and-retry on 401 |
| Realtime delivery | Push via SignalR groups (`user-{id}`, `office`, `trip-{id}`) | Subscribes to 3 of 5 server events as Dart `Stream`s; falls back to REST silently if the socket never connects |
| Pagination contract | `PagedResult<T>`-shaped responses, SQL-level `Skip/Take` on most list endpoints (Orders, Notifications, Admin orders/trips); a few endpoints intentionally aren't paginated at all (`Messages/conversations`, `Complaints/all`, `Complaints/violations`) and the mobile client mirrors that rather than inventing client-side paging | Generic `PagedResult` model + infinite-scroll (200px-before-end trigger) on Orders, Notifications, and the Admin Orders/Trips/Driver-Approvals dashboards |
| Enum contract | Serialized as **raw ints** by default (no global `JsonStringEnumConverter` is configured in `Program.cs`) — `NotificationType` is the *one* exception, explicitly annotated `[JsonConverter(typeof(JsonStringEnumConverter))]` on its DTO property | Every Dart enum mirrors the backend's int ordering exactly (`fromCode(int)` factories), not the name — confirmed by reading each backend enum's declaration order directly, since a mismatched order would silently corrupt data with no compiler error |
| Image serving | `/images/{fileName}` static files (content-root `Images/` folder) | Profile photo upload via `image_picker` + multipart `PUT /Passengers/update-profile` |

No contract mismatches were found between what the backend currently exposes and what the Flutter app currently calls, with one exception that was found and fixed during the Admin app build: `AdminController.GetTrips` was missing the `[FromQuery]` attribute its sibling `GetOrders` action has, so its filters/pagination would have silently no-op'd against query-string params (see `KNOWN_ISSUES.md`). The remaining gap is **unbuilt UI for already-shipped backend endpoints** (ratings submission, order editing, favorite locations, a passenger-side complaint-filing screen), not broken integration of what *is* built. See `TODO.md`.

---

## 5. Notable design decisions (and their rationale, where recoverable from code/comments)

- **No payment/fare model** — cash-only by product decision; nothing in the schema anticipates adding one without a real migration.
- **flutter_map over google_maps_flutter** — avoids needing a Google Maps API key and native Android/iOS manifest configuration that couldn't be verified in the original dev environment; documented as a presentation-layer-only swap if a key becomes available later.
- **Single SignalR hub for everything** rather than per-feature hubs — simpler connection management (one socket per client) at the cost of every client needing to filter/ignore events it doesn't care about (mitigated by group scoping).
- **Repositories instead of a dedicated Service layer** for most business logic — keeps the codebase smaller for its current size, at the cost of repositories doing more than data access (OTP generation, notification side-effects, dispatch scoring all live in repository methods).
- **Combined Order-detail/Trip-tracking screen** on mobile — a deliberate product-shape decision (see §2.6), not a shortcut.
- **String-based result branching** in several controllers/services (`DriverTripsController`, `PassengerTripsController` check `result.Contains("success")`-style strings rather than typed result enums) — a maintenance hazard flagged in `KNOWN_ISSUES.md`, not a deliberate design choice worth preserving.
