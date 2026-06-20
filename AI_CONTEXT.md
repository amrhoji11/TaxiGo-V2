# AI_CONTEXT — TaxiApp

Generated from a full read-through of the actual codebase (not chat history) on 2026-06-20.

Two repositories make up this product:
- **`TaxiApp.Backend`** (this repo) — ASP.NET Core 9 Web API, Clean-Architecture-style, 3 projects.
- **`TaxiApp.Mobile`** (sibling repo, `../TaxiApp.Mobile`) — Flutter client, currently a Passenger-only MVP.

---

## 1. Project overview

TaxiApp is a cash-only, phone+OTP taxi-hailing platform for three roles: **Passenger**, **Driver**, **Admin/Office**. There is no fare/price/payment model anywhere in the schema — this is a deliberate product decision, not a gap.

Core loop: a Passenger creates an `Order` with pickup/dropoff coordinates → the dispatch engine (`DriverAssignmentRepository`) finds and offers the trip to nearby available Drivers → a Driver accepts → a `Trip` is created/linked via `TripOrder` → status progresses (Assigned → DriverArrived → InProgress → Completed) with live location and notification push over SignalR → Passenger can rate the driver and chat with them per-order.

An Admin/Office role oversees driver approval, manual dispatch override, vehicle fleet management, complaints/violations, user blocking, and reporting dashboards.

Passenger, Driver, and Admin all have a built mobile MVP now (Admin as of 2026-06-20, graduation-demo scope — see TODO.md #12 for what's still missing from it).

## 2. Technologies and versions

### Backend (`TaxiApp.Backend`)
- **.NET 9** (`net9.0`) across all 3 projects, nullable + implicit usings enabled.
- **TaxiApp.Backend** (API): Mapster 7.4.0, Microsoft.AspNetCore.Authentication.JwtBearer 9.0.13, Microsoft.AspNetCore.OpenApi 9.0.4, Microsoft.EntityFrameworkCore.Design 9.0.12, Swashbuckle.AspNetCore 9.0.5.
- **TaxiApp.Backend.Core** (models/DTOs/interfaces, no project references): Microsoft.AspNetCore.Http.Features 2.3.0, Microsoft.AspNetCore.Identity.EntityFrameworkCore 9.0.12, System.IdentityModel.Tokens.Jwt 8.15.0.
- **TaxiApp.Backend.Infrastructure** (repositories/EF/SignalR, references Core): Microsoft.AspNetCore.Identity.EntityFrameworkCore 9.0.12, Microsoft.AspNetCore.SignalR 1.2.9, Microsoft.EntityFrameworkCore.SqlServer 9.0.12, Microsoft.EntityFrameworkCore.Tools 9.0.12, Twilio 7.14.7.
- Database: SQL Server (`Microsoft.EntityFrameworkCore.SqlServer`), 13 EF Core migrations, latest `20260503125644_updateAuth`.
- Realtime: ASP.NET Core SignalR (single hub).
- SMS/OTP delivery: Twilio.
- Maps/geocoding: a `GoogleMapService` behind `IMapService`, registered via `AddHttpClient`.

### Mobile (`TaxiApp.Mobile`)
- Dart SDK `>=3.5.0 <4.0.0`, Flutter `>=3.24.0` (toolchain in use: Flutter 3.44.2 / Dart 3.12.2).
- State management: `flutter_riverpod ^2.5.1` (`Notifier`/`FamilyNotifier`, no codegen).
- Networking: `dio ^5.4.3+1`.
- Routing: `go_router ^14.2.0`.
- Secure storage: `flutter_secure_storage ^9.2.2`.
- Maps: `flutter_map ^8.3.0` + `latlong2 ^0.9.1` (OpenStreetMap tiles — chosen specifically to avoid needing a Google Maps API key/native manifest config).
- Realtime: `signalr_netcore ^1.4.4`.
- Other: `pinput` (OTP entry), `intl`, `image_picker`.
- No `build_runner`/`freezed`/`json_serializable` — all JSON (de)serialization is hand-written.
- Lints: `flutter_lints ^4.0.0` + 3 custom rules (`prefer_single_quotes`, `sort_constructors_first`, `require_trailing_commas`). `flutter analyze` is currently clean (0 issues).

## 3. Backend structure

```
TaxiApp.Backend.sln
├── TaxiApp.Backend/                 (API — Controllers, Program.cs, appsettings)
├── TaxiApp.Backend.Core/            (Models, DTO'S, Interfaces, JwtService, Settings)
└── TaxiApp.Backend.Infrastructure/  (Data/ApplicationDbContext, Repositories, Helper, Migrations)
```

Dependency direction is one-way and clean: `Core` has zero project references → `Infrastructure` references `Core` → `Api` references `Infrastructure`. One violation exists: `NotificationsController` injects `ApplicationDbContext` directly (see KNOWN_ISSUES.md).

**19 controllers** under `TaxiApp.Backend/Controllers/`: AccountController, AdminController, BaseController (shared access-check base), ComplaintsController, DriverApprovalsController, DriverAssignmentManualController, DriverTripsController, DriversController, FavoriteLocationsController, MessagesController, NotificationsController, OrdersController, PassengerTripsController, PassengersController, SettingsController, TestsController (empty scaffold), UserBlocksController, UsersController, VehiclesController.

**21 repositories** under `TaxiApp.Backend.Infrastructure/Repositories/`, each behind an interface in `Core/Interfaces/`. Notable ones: `DriverAssignmentRepository` (the ~2,100-line dispatch engine — scoring/offering/timeout logic), `AdminAssignmentRepository` (manual override + Auto/Manual mode), `AuthRepository` (OTP + JWT + refresh-token rotation), `DriverTrackingRepository` (location ingestion + SignalR broadcast), `NotificationRepository`, `MessageRepository`.

**3 background services** (`IHostedService`) registered in `Program.cs`: `TripOfferBackgroundService` (offer-timeout sweep / delay detection), `RefreshTokenCleanupService`, `DatabaseCleanupService` (data retention purges).

## 4. Flutter structure

```
TaxiApp.Mobile/lib/
├── core/
│   ├── constants/         (api_constants.dart — base URL via --dart-define, storage_keys.dart)
│   ├── models/             (shared models e.g. PagedResult)
│   ├── network/            (dio_client, auth_interceptor, logging_interceptor, api_exception)
│   │   └── interceptors/
│   ├── providers/          (core_providers.dart — cross-cutting DI wiring)
│   ├── realtime/           (realtime_service.dart — SignalR wrapper)
│   ├── router/             (app_router.dart — go_router + auth redirect)
│   ├── storage/            (secure_storage_service.dart)
│   ├── theme/
│   ├── utils/
│   └── widgets/
└── features/
    ├── admin/       (Orders/Trips dashboards, Driver approvals, Manual assignment + dispatch-mode toggle, Complaints/Violations)
    ├── auth/        (data/domain/presentation — login, OTP, registration, role-select)
    ├── chat/        (conversations list + per-order message thread, role-agnostic)
    ├── driver/       (Dashboard/Queue/Offer/ActiveTrip — one adaptive screen — + Driver Profile)
    ├── notifications/
    ├── orders/      (home/create-order, orders list, combined order-detail+tracking; also the shared OrderStatus/TripStatus/VehicleSize/OrderRating/GeoPoint entities the Admin feature reuses)
    ├── profile/      (Passenger profile)
    └── shell/       (presentation only — passenger_shell_screen.dart / driver_shell_screen.dart / admin_shell_screen.dart, bottom-nav hosts)
```

Each feature follows `data/{datasources,models,repositories}` → `domain/{entities,repositories}` → `presentation/{providers,screens,state,widgets}`. State is exposed through sealed state classes (`AuthState`, `OrderDetailState`, `OrdersListState`, `NotificationsListState`, `ProfileState`, `DriverActiveUiState`, `DriverProfileState`, `ConversationsListState`, `ConversationDetailState`, `OrdersDashboardState`, `TripsDashboardState`, `DriverApprovalsState`, `ManualAssignmentState`, `ComplaintsState`) consumed via Dart 3 `switch` expressions.

**Screens implemented**:
- *Passenger*: splash, welcome, login, OTP entry, role-select, registration form, driver-blocked notice, passenger shell (bottom nav: Home/Orders/Messages/Notifications/Profile), home/create-order (map-tap pickup/dropoff), orders list, order detail (merged with live trip tracking), notifications, profile, conversations list + conversation detail (chat).
- *Driver*: driver shell (bottom nav: Dashboard/Messages/Notifications/Profile), Dashboard (one adaptive screen covering idle/Enter-Queue, incoming order offer with Accept/Reject, and active trip with Arrived/Pickup/Dropoff), driver profile, conversations list + conversation detail (same chat feature as Passenger).
- *Admin*: admin shell (bottom nav: Orders/Trips/Approvals/Assignment/Complaints — no Profile tab in this MVP; logout is a toolbar action on every tab instead), Orders dashboard (office-wide, status filter + search + pagination), Trips dashboard (same shape), Driver Approvals (pending queue + approve/reject + a tap-through detail sheet), Manual Assignment (assign-driver-to-order/assign-driver-to-trip + the Auto/Manual dispatch-mode toggle), Complaints (complaints + violations as two tabs, status updates, complaint→violation escalation).

**Not yet built**: rating-submission UI, favorite-locations UI, complaints UI *for passengers* (filing one — the Admin side that reviews them is now built), order-editing UI, settings screen, phone-change-flow UI, driver leave-queue/go-offline (no backend endpoint exists for it either), Admin vehicle-fleet management, Admin user-blocking UI, Admin top-drivers leaderboard, an Admin profile screen — see TODO.md.

## 5. Database design

EF Core `ApplicationDbContext : IdentityDbContext<ApplicationUser>`. DbSets: `Drivers`, `DriverApprovals`, `Complaints`, `Violations`, `SystemSettings`, `DriverLocations`, `Messages`, `Notifications`, `OfficeQueueEntries`, `Orders`, `OrderReviews`, `Passengers`, `Ratings`, `Trips`, `TripOrders`, `UserBlocks`, `Vehicles`, `RefreshTokens`, `FavoriteLocations` (plus ASP.NET Identity's own Users/Roles/Claims/Logins/Tokens tables).

**Core entities and relationships:**
- `ApplicationUser` (extends `IdentityUser`) — 1:1 with `Passenger` and `Driver`; 1:N `SentMessages`/`ReceivedMessages`/`Notifications`/`Reviews`/`RatingsGiven`/`RatingsReceived`.
- `Passenger` (PK = `UserId`, shared with User) → 1:N `Orders`.
- `Driver` (PK = `UserId`) → 1:N `Vehicles`, `Trips`, `Locations`; `DriverStatus` enum (available/busy/Shared/offline/rejected); soft-delete via `IsDeleted` query filter.
- `Order` (PK `OrderId`, FK `PassengerId`) — pickup/dropoff lat/lng, `OrderPriority` (Normal/Urgent), `RequiredVehicleSize`, `OrderStatus` (Pending/SearchingDriver/PendingOfficeReview/AssignedToTrip/Cancelled/Completed/NoDriverFound), dispatch bookkeeping fields (`TripOfferSentAt`, `LastOfferedDriverId`, `IsManuallyAssigned`, `ExpectedArrivalAt`/`IsDelayNotified`).
- `Trip` (PK `TripId`, nullable FK `DriverId`) — `TripStatus` enum, same dispatch bookkeeping fields as Order, 1:N `Ratings`/`Locations`/`Messages`.
- `TripOrder` — composite PK (`TripId`+`OrderId`), join entity with its own `TripOrderStatus` (Assigned/PickedUp/DroppedOff/Cancelled/Unassigned/DriverArrived).
- `Vehicle` (PK `VehicleId`, nullable FK `DriverId`) — `VehicleSize` enum, `IsCurrent` enforced unique-per-driver via filtered index.
- `DriverApproval` (PK `DriverId`, 1:1 with Driver) — `ApprovalStatus` (pending/approved/rejected).
- `DriverLocation` — Lat/Lng as `decimal(18,8)`, indexed `RecordedAt` for purge jobs.
- `Rating` — `Stars` (`[Range(1,5)]`), unique index on (TripId, RaterUserId, TargetUserId).
- `Notification` — `NotificationType` enum (16 values incl. RateTrip, MessageReceived, TripAssigned, DelayWarning, DriverApprovalPending, Violation, Complaint, …).
- `Message` — Sender/Receiver FK→ApplicationUser (Restrict delete), optional Order/Trip FK (SetNull delete), `[MaxLength(1000)]` body.
- `Complaint` / `Violation` — **no FK/navigation properties at all**; `SenderId`/`AgainstUserId`/`DriverId`/`OrderId`/`TripId` are bare columns with no DB-enforced referential integrity (see KNOWN_ISSUES.md).
- `UserBlock` — time-windowed (`StartsAt`/`EndsAt`).
- `FavoriteLocation` — has a `User` nav property but no explicit `[ForeignKey]`/`OnModelCreating` config (relies on EF convention, unlike every other relationship in the model).
- `RefreshToken` — stores only a SHA-256 `TokenHash` (never the raw token), rotation chain via `ReplacedByTokenHash`.
- `SystemSettings` — singleton row, `SystemMode` (Auto/Manual) driving dispatch behavior.
- `OfficeQueueEntry` — driver office-queue check-in/out, one active entry per driver enforced via filtered unique index.
- `OrderReview` — office review queue for flagged orders.

No Fare/Price/Payment/Amount field exists anywhere — confirmed, matches the cash-only product decision.

## 6. Authentication flow

Phone + OTP, **passwordless** (Identity's password requirements are configured to effectively no-op — see KNOWN_ISSUES.md).

1. **Register**: `POST /Account/registerPassenger` or `/registerDriver` → OTP cached in `IMemoryCache` (5–10 min TTL) alongside pending registration data → `POST /Account/confirm-register-{role}` creates the `ApplicationUser` via `UserManager`, assigns the role, creates the `Passenger`/`Driver` row (+ `DriverApproval` row with `pending` status for drivers).
2. **Login**: `POST /Account/login` (rate-limited 5/min/IP) → phone lookup → driver-approval gate (blocks pending/rejected drivers, returns a classifiable message) → OTP sent via Twilio SMS, 60s resend cooldown.
3. **Verify**: `POST /Account/verify-otp` (rate-limited 5/5min, partitioned by user-or-IP) → brute-force guard (5 attempts / 2-min lockout) → on success, issues a JWT (8-hour expiry; claims: `Name`, `MobilePhone`, `Role`, `NameIdentifier` — **no claim literally named `"UserId"`**) + a cryptographically random 64-byte refresh token (stored only as its SHA-256 hash).
4. **Refresh**: `POST /Account/refresh-token` — rotates the token; if a previously-revoked token is replayed (theft signal), **all** of that user's active refresh tokens are revoked.
5. **Roles**: `SuperAdmin`, `Admin`, `Driver`, `Passenger` seeded at startup. `SuperAdmin` is seeded but never assigned/referenced anywhere — dead role.
6. JWT validated with full issuer/audience/lifetime/signing-key checks and `ClockSkew = 0`. A custom `OnMessageReceived` handler extracts the token from the `?access_token=` query string specifically for `/notificationHub` paths, since the SignalR WebSocket handshake can't carry a custom `Authorization` header from a browser/mobile client.

Mobile side: `flutter_secure_storage` persists `access_token`/`refresh_token`/`user_id`/`user_role`. `auth_interceptor.dart` attaches the bearer token to every request, and on a 401 performs a single coalesced refresh-and-retry (concurrent 401s share one in-flight refresh call) before forcing logout via a `sessionExpiredTickProvider`.

## 7. API integrations

- **Twilio** — OTP SMS delivery (`Twilio:AccountSid/AuthToken/FromPhone` in config; placeholders in source, must be overridden per environment).
- **Google Maps** — `GoogleMapService : IMapService`, used server-side for geocoding/ETA (`GoogleMaps:ApiKey` placeholder in config). Note: the *mobile app* does not use Google Maps — it uses `flutter_map`/OpenStreetMap independently, so the backend's Google Maps key only matters for server-side routing/ETA calculations, not the client map UI.
- **SignalR hub** (`/notificationHub`, `[Authorize]`) — the single realtime channel for everything: notifications, trip status, driver location, chat messages, route updates. Client→Server: `SendLocation`, `JoinTrip`, `LeaveTrip`. Server→Client: `ReceiveNotification`, `UpdateTripStatus`, `DriverLocationUpdated`, `RouteUpdated`, `ReceiveMessage`. On connect, every client auto-joins `user-{id}`; Admins additionally join a shared `office` group.
- Mobile `RealtimeService` consumes 4 of those 5 events as broadcast streams (`ReceiveNotification`, `UpdateTripStatus`, `DriverLocationUpdated`, `ReceiveMessage`) — it does not subscribe to `RouteUpdated` (not applicable to passengers currently, see KNOWN_ISSUES.md). `ReceiveMessage` is pushed to both `user-{receiverId}` and `trip-{tripId}`, so chat consumers (`ConversationDetailController`) dedupe incoming messages by `messageId`.

## 8. Completed features

**Backend** (functional, end-to-end):
- Passenger & Driver registration/login via phone+OTP, JWT + refresh-token rotation with theft detection.
- Driver onboarding/approval workflow.
- Order lifecycle: create/edit/cancel/detail/list.
- Dispatch: accept/reject order or trip, arrive/start/pickup/dropoff/cancel, office queue entry.
- Manual admin dispatch override + Auto/Manual mode toggle.
- Vehicle fleet management with photo upload.
- Notification history + live SignalR push + mark-read.
- Per-order messaging/chat with live delivery and read receipts.
- Driver ratings (1–5 stars, time-windowed, duplicate-protected).
- Complaints → violations escalation feeding driver scoring.
- User moderation: time-windowed blocking, soft-delete/restore for drivers and passengers.
- Favorite locations CRUD.
- User-facing settings (language, dark mode, notification toggle).
- Admin reporting: paginated/filterable/sortable orders & trips dashboards, top-drivers leaderboard.
- Background automation: offer-timeout sweep, delayed-trip alerts, refresh-token cleanup, data-retention purges.
- Image upload + serving (`/images/{fileName}`).

**Mobile** (Passenger + Driver, wired to real endpoints):
- Auth: OTP login, passenger/driver registration, driver-approval gating, logout, transparent token refresh, session restore.
- Orders (Passenger): create (map-tap pickup/dropoff, vehicle size, passenger count, urgency), paginated history, combined detail+live-tracking, cancel.
- Dispatch (Driver): one adaptive Dashboard covering idle/Enter-Queue, incoming order offer (Accept/Reject, live countdown), and active trip (Arrived/Pickup/Dropoff per stop).
- Notifications: paginated center with live push, mark-as-read/mark-all-read, unread badge — shared by Passenger and Driver.
- Chat: conversations list + per-order message thread, paginated, live over SignalR (deduped by `messageId`) — shared by Passenger and Driver, reachable from a Messages tab on both shells plus a quick-access icon on Order Detail / the active-trip stop card.
- Profile: view/edit name/address, photo upload, logout — separate screens for Passenger (`profile/`) and Driver (`driver/`, includes vehicle/approval/queue status).

**Mobile (Admin, graduation-demo MVP, 2026-06-20)**:
- Orders/Trips dashboards: office-wide (every passenger's/driver's, not scoped to one user), status filter, search, newest-first pagination — reuses the Passenger app's `OrderStatus`/`TripStatus`/`VehicleSize`/`OrderRating` entities rather than redefining them.
- Driver Approvals: pending-queue list with approve/reject, a tap-through detail sheet (name/contact/vehicle-model-and-plate/trip-stats/rating — the backend has no document/photo fields to show, see KNOWN_ISSUES.md #16).
- Manual Assignment: assign a specific driver to an order or a whole trip (overriding auto-dispatch), plus the office-wide Auto/Manual dispatch-mode toggle on the same screen. Both assign actions surface the backend's bare result string verbatim, since success/failure can't be told apart by HTTP status (always 200).
- Complaints: two tabs (complaints, violations) backed by one screen; update a complaint's status, optionally escalate it into a violation against the accused driver in the same action, resolve violations.
- Required 3 backend changes (see §3/KNOWN_ISSUES.md): `AdminController.GetTrips` was missing `[FromQuery]` so its filters silently no-op'd; added `GET /DriverAssignmentManual/assignable-drivers` (no prior endpoint returned drivers with names); enriched `GET /Complaints/all` with resolved sender/against-user display names instead of bare GUIDs (new `ComplaintDto`/`ViolationDto`).

## 9. Current implementation status

| Area | Backend | Mobile (Passenger) | Mobile (Driver) | Mobile (Admin) |
|---|---|---|---|---|
| Auth | ✅ | ✅ | ✅ | ✅ (same OTP flow; no Admin profile screen, logout is a toolbar action) |
| Orders | ✅ (incl. edit) | ✅ (no edit UI) | n/a | ✅ (read-only office-wide dashboard; no edit UI) |
| Dispatch / trip actions | ✅ | n/a | ✅ (single-order flow; no separate Start-Trip button, Pickup covers it) | ✅ (manual assign-to-order/assign-to-trip + Auto/Manual toggle) |
| Notifications | ✅ | ✅ | ✅ | ❌ (no Admin notifications screen in this MVP) |
| Chat/Messaging | ✅ | ✅ | ✅ | n/a (Admin doesn't have a chat surface) |
| Ratings | ✅ (submit via backend; display only on mobile) | display only | n/a | display only (within Trips dashboard rows / driver-approval detail) |
| Complaints/Violations | ✅ | ❌ (no filing UI) | ❌ | ✅ (review/status-update/escalate/resolve) |
| Vehicle management | ✅ | n/a | view-only (no self-edit; Admin-only on backend) | ❌ (no fleet-management screen yet) |
| Favorite locations | ✅ | ❌ | n/a | n/a |
| Settings | ✅ | partial (no settings screen) | ❌ | ❌ |
| Office queue | ✅ enter-queue only (no leave/go-offline endpoint exists) | n/a | enter-queue only, matching the backend gap | n/a (Admin doesn't manage individual queue entries) |
| Admin reporting/dashboards | ✅ | n/a | n/a | ✅ (Orders/Trips dashboards; top-drivers leaderboard not yet surfaced) |
| Driver approvals | ✅ | n/a | n/a (driver sees approval status, not the queue) | ✅ (pending queue, approve/reject, detail sheet) |
| User blocking | ✅ (`UserBlocksController`, currently broken — KNOWN_ISSUES.md #1) | n/a | n/a | ❌ (no UI yet, and the backend action can't succeed regardless) |

See `TODO.md` for the prioritized backlog and `KNOWN_ISSUES.md` for bugs/risks found during this analysis.
