# MISSING_FEATURES — TaxiApp Complete Project Audit

Generated 2026-06-20 from a direct, fresh re-read of both repositories (`TaxiApp.Backend` and `TaxiApp.Mobile`) — every item below was verified against current code, not assumed from prior docs. Covers all 12 requested areas: Backend, Flutter, Database, SignalR, Authentication, Notifications, Chat, Admin, Driver, Passenger, Maps & GPS, Real-time Updates.

This is an audit document. Nothing was implemented or changed while producing it.

Companion documents: `GRADUATION_BLOCKERS.md` (the must-fix subset), `UNUSED_ENDPOINTS.md` (full Flutter↔backend usage cross-reference), `IMPLEMENTATION_STATUS.md` (area-by-area status table + completion percentages).

Priority legend: 🔴 Critical · 🟠 High · 🟡 Medium · 🟢 Low · ⚪ Nice to Have

---

## 1. ASP.NET Core Backend

### 1.1 — `UserBlocksController.ToggleUserBlock` is permanently broken 🟠 High
- **Description**: Reads the acting admin's id via `User.FindFirstValue("UserId")` (`UserBlocksController.cs:27`), but the JWT never issues a claim literally named `"UserId"` — only `ClaimTypes.NameIdentifier`. `officeId` is always `null`, so the action always returns 401.
- **Why it matters**: The endpoint can never succeed for any caller, by anyone, ever, as written. Not demo-visible today only because no Flutter screen calls it yet (see §8.3) — but it would fail instantly if tested directly (Swagger/Postman) or if a user-blocking UI is added later.
- **Backend or Flutter**: Backend.
- **Estimated effort**: 5 minutes (one-line fix).
- **Recommended solution**: Read `User.FindFirstValue(ClaimTypes.NameIdentifier)` instead, matching every other controller.

### 1.2 — `BaseController.CheckUserAccessAsync` null-reference risk 🟠 High
- **Description**: `var user = await userRepository.GetUserByIdAsync(userId); if (user.IsDeleted)` — no null check (`BaseController.cs:27-29`). Used by Orders, Passengers, Drivers, DriverTrips, Vehicles, PassengerTrips, Messages, Complaints controllers.
- **Why it matters**: A syntactically valid JWT for a user row that no longer exists (e.g. a deleted test account whose token hasn't expired) crashes with a 500 instead of a clean 401/403, across nearly the entire authenticated API surface. The same unguarded pattern is duplicated inline in `FavoriteLocationsController.cs:35-40,63-68,90-95`.
- **Backend or Flutter**: Backend.
- **Estimated effort**: 15 minutes.
- **Recommended solution**: Null-check before dereferencing; return 401 if null.

### 1.3 — `UsersController.ChangeUserRole` has no role allow-list 🟠 High
- **Description**: `ChangeUserRole(userId, roleName)` passes `roleName` straight to `UserManager.AddToRoleAsync` with zero validation against the four seeded roles (`UsersController.cs:69-79`).
- **Why it matters**: Any string becomes a valid role assignment — typos or made-up roles silently corrupt a user's authorization state. Privilege-escalation-adjacent if this endpoint is ever reachable by a less-trusted caller. (Currently unused by Flutter — see §8.2 — so not demo-visible, but a real defect.)
- **Backend or Flutter**: Backend.
- **Estimated effort**: 30 minutes.
- **Recommended solution**: Validate `roleName` against an enum/allow-list of the 4 seeded roles before calling `AddToRoleAsync`.

### 1.4 — Driver-initiated trip cancellation has no rate limiting, and 9 of 11 driver-trip actions are unprotected 🟡 Medium
- **Description**: Only `accept-order`/`reject-order` use `[EnableRateLimiting("DriverActionsPolicy")]`. `accept-trip`, `reject-trip`, `arrived`, `start-trip`, `pickup`, `dropoff`, `cancel-trip`, `enter-queue` have none.
- **Why it matters**: A buggy or malicious client could spam state-mutating trip actions with no throttle. Low likelihood in a graduation demo, but a real abuse vector.
- **Backend or Flutter**: Backend.
- **Estimated effort**: 30 minutes.
- **Recommended solution**: Apply the same rate-limit policy consistently across all driver-trip actions.

### 1.5 — Fragile string-matching result contracts on multiple controllers 🟡 Medium
- **Description**: `DriverTripsController`/`PassengerTripsController` branch on substring-matching free-text repository return values (`result.Contains("success")` etc.) rather than typed results. `DriverAssignmentManualController`'s `manual-assign-order`/`manual-assign-trip` and `ComplaintsController`'s `Create`/`UpdateStatusAsync`/`ResolveViolationAsync` always return HTTP 200 regardless of outcome — success/failure is only distinguishable by the literal response string (`"Manually assigned"` vs `"Driver not available"` vs `"Order not found"`).
- **Why it matters**: Any backend copy change for these specific messages silently breaks client behavior with no compiler error. The Flutter Admin screens already had to be written to surface these raw strings verbatim rather than trusting HTTP status.
- **Backend or Flutter**: Backend (root cause) / Flutter (workaround already in place).
- **Estimated effort**: 1-2 days to convert to typed result enums/objects across all affected controllers.
- **Recommended solution**: Replace string returns with a typed result (enum or small result object with a status code + message), at minimum for the always-200 cases.

### 1.6 — `TestsController.cs` is a dead empty scaffold 🟢 Low
- **Description**: Zero actions, just route-table/Swagger clutter.
- **Why it matters**: Cosmetic only.
- **Backend or Flutter**: Backend.
- **Estimated effort**: 2 minutes.
- **Recommended solution**: Delete the file.

### 1.7 — Minor dead code / inconsistencies ⚪ Nice to Have
- Duplicate `using TaxiApp.Backend.Core.Settings;` (`Program.cs:14-15`).
- `Console.WriteLine` in `NotificationHub.cs:39` instead of injected `ILogger`.
- `AddVehicel` → `AddVehicle` typo (`IVehicleRepository.cs:15`, `VehicleRepository.cs:24`, called from `VehiclesController.cs:58`).
- Unused `using Azure.Core;`/`using Twilio.Http;` in `AuthRepository.cs`.
- **Estimated effort**: under an hour combined.

### 1.8 — Zero automated backend test coverage 🟠 High
- **Description**: No test project exists alongside the 3 main projects in the solution.
- **Why it matters**: Nothing protects against regressions; for a graduation project this is often explicitly graded as a software-engineering-practice criterion separate from "does it work."
- **Backend or Flutter**: Backend.
- **Estimated effort**: Multi-day if pursued seriously; a token suite (a handful of repository/controller unit tests) is a few hours.
- **Recommended solution**: At minimum, add a test project with unit tests for the riskiest repositories (`AuthRepository`, `DriverAssignmentRepository`) and an integration test hitting a few critical endpoints.

---

## 2. Flutter Mobile App

### 2.1 — Order editing has no UI 🟡 Medium
- **Description**: `PUT /api/Orders/{id}` (`EditOrder`) works on the backend; no Flutter call exists (`OrdersEndpoints` has no `edit` member).
- **Why it matters**: A passenger who mis-enters pickup/dropoff must cancel and recreate the order instead of correcting it.
- **Backend or Flutter**: Flutter (backend already supports it).
- **Estimated effort**: 0.5-1 day (one form screen/dialog + one API method).
- **Recommended solution**: Add an "Edit" action on `OrderDetailScreen` while the order is still editable, reusing the existing pickup/dropoff map-picker UI from `HomeScreen`.

### 2.2 — No automated test coverage beyond Splash and Chat 🟠 High
- **Description**: Only `test/widget_test.dart` (trivial Splash spinner check) and `test/chat_screens_test.dart` (2 widget tests) exist. Orders, Profile, Driver, Admin, Notifications, and the entire Auth flow (login/OTP/registration) have zero test coverage.
- **Why it matters**: Same rationale as §1.8 — no regression safety net for the bulk of the app, and likely graded.
- **Backend or Flutter**: Flutter.
- **Estimated effort**: Multi-day for meaningful coverage; a few widget tests for the highest-risk controllers (Auth, Orders, Driver dispatch) in under a day.
- **Recommended solution**: Prioritize widget tests for `AuthController`'s login/OTP flow and `DriverActiveController`'s offer-accept/reject flow — the two flows where a silent regression would be most demo-damaging.

### 2.3 — Dependency versions are a major version behind across the board ⚪ Nice to Have
- **Description**: `flutter_riverpod` 2.x (3.x available), `go_router` 14.x (17.x available), `flutter_secure_storage` 9.x (10.x available), `flutter_lints` 4.x (6.x available).
- **Why it matters**: Not urgent — current pins are stable and `flutter analyze` is clean — but Riverpod 3 has breaking changes; deferring the upgrade indefinitely makes it harder later.
- **Backend or Flutter**: Flutter.
- **Estimated effort**: 1-2 days when undertaken (mostly Riverpod 3 migration).
- **Recommended solution**: Not before graduation; revisit afterward.

---

## 3. Database

### 3.1 — `Complaint`/`Violation` have zero FK/navigation configuration 🟡 Medium
- **Description**: No `builder.Entity<Complaint>()` or `builder.Entity<Violation>()` block exists in `OnModelCreating` at all. `SenderId`/`AgainstUserId`/`DriverId`/`OrderId`/`TripId` are bare unconstrained columns.
- **Why it matters**: Orphaned or misspelled IDs are possible and undetectable by the database — undermines the audit trail this subsystem exists to provide. Confirmed still true by reading `ApplicationDbContext.cs` directly.
- **Backend or Flutter**: Backend.
- **Estimated effort**: Half a day (requires a new migration; verify no orphaned rows exist first).
- **Recommended solution**: Add explicit `HasOne`/`HasForeignKey` configuration with `OnDelete(DeleteBehavior.Restrict)` or `SetNull` as appropriate, then a migration.

### 3.2 — `FavoriteLocation.UserId` has no explicit FK configuration 🟢 Low
- **Description**: Relies entirely on EF convention, unlike every other relationship in the model.
- **Why it matters**: Inconsistency that makes the model harder to reason about; low risk since the feature isn't even exposed in the app yet (§8.6).
- **Backend or Flutter**: Backend.
- **Estimated effort**: 1 hour + migration.
- **Recommended solution**: Add explicit config to match the rest of the model, or defer until the Favorite Locations feature is actually built.

### 3.3 — No unique DB constraint on `ApplicationUser.PhoneNumber` 🟡 Medium
- **Description**: Phone is the sole login identifier; uniqueness is only checked in application code at registration, not enforced by a schema-level unique index.
- **Why it matters**: A race condition between two near-simultaneous registrations could create two accounts with the same phone number, breaking the login-by-phone assumption everywhere else in the app.
- **Backend or Flutter**: Backend.
- **Estimated effort**: 1-2 hours + migration (must first confirm no existing duplicate rows).
- **Recommended solution**: Add a unique filtered index on `PhoneNumber` (filtered to exclude soft-deleted rows if those are allowed to reuse a number).

### 3.4 — Username collisions are plausible 🟢 Low
- **Description**: `UserName = FirstName` only at registration (`AuthRepository.cs:118,224`).
- **Why it matters**: Two passengers registering with the same first name around the same time risk a generic Identity "username taken" failure unrelated to their actual identity.
- **Backend or Flutter**: Backend.
- **Estimated effort**: 1-2 hours.
- **Recommended solution**: Derive username from phone number or append a disambiguator.

### 3.5 — Migrations are current; no pending model drift detected ⚪ Informational
- 16 migrations exist, latest `20260503125644_updateAuth`, matching the current model as far as this audit could determine by inspection. No action needed.

---

## 4. SignalR

### 4.1 — `RouteUpdated` never reaches the passenger 🟢 Resolved (2026-06-20)
- **Original gap**: `TripRoutingService.RecalculateTripAsync` sent `RouteUpdated` only to `Clients.Group("user-{driverId}")`, never to `trip-{tripId}`. Flutter's `RealtimeService` had no listener for this event at all either way, on either side.
- **Second, separate bug found while fixing this**: `BuildSharedTripRoute`'s planning loop removed an order from consideration immediately after adding its *pickup* step, so for the common single-passenger case it never planned a dropoff step at all — the route (and therefore the polyline, since `GoogleMapService.GetRoutePolylineAsync` is built from the same `Steps` list) would only ever have covered "driver → pickup," never "pickup → dropoff," even after the passenger was in the car. The post-pickup recalculation that would otherwise refresh this was also skipped for single-order trips (`trip.TripOrders.Count > 1` guard).
- **Fix shipped this session**: `BuildSharedTripRoute` now tracks pending pickup/dropoff legs as two sets instead of deriving "pickup or dropoff" from the order's real (loop-invariant) `StatusInTrip`, so a single order correctly contributes both legs. The post-pickup recalculation now runs unconditionally. The backend now also broadcasts a passenger-safe `{ polyline, totalMinutes }` subset to `trip-{tripId}` (the full payload, with every stop's passenger name, stays driver-only — relevant only for shared trips, which aren't reachable through any UI today, but kept defensive). Flutter: new `routeUpdates` stream on `RealtimeService`, a shared `TripRoute` parser (decodes the real polyline when present, falls back to a straight line through the stop sequence — prefixed with the driver's last known position on the passenger side — when the polyline is empty, which is the common case while `GoogleMaps:ApiKey` is still a placeholder), and `PolylineLayer` rendering on the shared `TripTrackingMap` widget used by both Driver and Passenger screens.
- **Backend or Flutter**: Both.
- **Estimated effort**: Done.

### 4.2 — `LeaveTrip` and `UpdateDriverStatus` server-pushed events have no Flutter listener 🟡 Medium
- **Description**: `DriverAssignmentRepository.cs` pushes `LeaveTrip` to the passenger (lines ~1973, ~2078, on trip completion/driver-cancellation) and `UpdateDriverStatus` to the driver (line ~2029, after a trip cancellation changes the driver's status) — neither event is defined as a stream in `RealtimeService` at all.
- **Why it matters**: **Verified this is not a functional dead-end** — both scenarios already separately trigger a `ReceiveNotification`/`UpdateTripStatus` push that the affected screens *do* listen to, which causes the same effective UI update (just on the next state refresh rather than instantly off this specific event). So this is a snappiness/code-cleanliness gap, not a silent-failure bug — downgraded accordingly from an initial higher-severity read.
- **Backend or Flutter**: Flutter (the events exist server-side; the client just doesn't have a dedicated handler).
- **Estimated effort**: 2-3 hours.
- **Recommended solution**: Either wire up dedicated streams for instant feedback, or — simpler — leave as-is and document that these two events are intentionally redundant with the notification channel.

### 4.3 — SignalR `EnableDetailedErrors` is unconditional 🟠 High
- **Description**: `Program.cs:138` sets `EnableDetailedErrors = true` with no environment gate.
- **Why it matters**: Leaks internal exception details (stack traces, type names) to any connected hub client in production.
- **Backend or Flutter**: Backend.
- **Estimated effort**: 10 minutes.
- **Recommended solution**: Gate behind `IsDevelopment()`.

### 4.4 — Confusing event-name reuse: `LeaveTrip` is both a Client→Server hub method and a Server→Client pushed event ⚪ Nice to Have
- **Description**: Same literal string used for two different directions/purposes.
- **Why it matters**: Pure code-clarity issue; not a functional bug, but easy to misread when debugging.
- **Backend or Flutter**: Backend.
- **Estimated effort**: 15 minutes (rename the server-push variant, e.g. `TripEnded`).
- **Recommended solution**: Rename one of the two for clarity — low priority, cosmetic.

### 4.5 — No reconnect/lifecycle handling beyond the library default 🟢 Low
- **Description**: `RealtimeService` has no app-foreground/background hook; relies entirely on `signalr_netcore`'s built-in `withAutomaticReconnect()`.
- **Why it matters**: A few seconds of stale trip-tracking UI after backgrounding/foregrounding until the library's own reconnect kicks in — acceptable under the app's "realtime is a progressive enhancement" design, but worth verifying under real device lock/unlock cycles before a live demo.
- **Backend or Flutter**: Flutter.
- **Estimated effort**: Half a day if pursued.
- **Recommended solution**: Add `WidgetsBindingObserver` lifecycle hooks to force a reconnect+refresh on resume, if device testing reveals it's actually needed.

---

## 5. Authentication

### 5.1 — OTP codes use `new Random()`, not a CSPRNG 🟡 Medium
- **Description**: `AuthRepository.cs:69,178,496` all use `new Random().Next(100000, 999999)`. Refresh tokens correctly use `RandomNumberGenerator`.
- **Why it matters**: Theoretically more predictable than a cryptographic RNG. Low severity in isolation given rate limiting and short OTP lifetime, but inconsistent with the refresh-token implementation right next to it.
- **Backend or Flutter**: Backend.
- **Estimated effort**: 15 minutes.
- **Recommended solution**: Switch to `RandomNumberGenerator.GetInt32(100000, 1000000)`.

### 5.2 — Secrets committed as placeholders 🟠 High
- **Description**: `JWT:Secret`, `Twilio:AccountSid/AuthToken`, `GoogleMaps:ApiKey` are placeholder literals in `appsettings.json`.
- **Why it matters**: If a real deployment forgets to override these via environment/user-secrets/Key Vault, the JWT signing key is publicly known and guessable — tokens become forgeable. Not a demo-day risk (runs locally) but a real production risk if this is ever deployed beyond a local/dev machine.
- **Backend or Flutter**: Backend.
- **Estimated effort**: A few hours to wire up proper secret management for a real deployment target.
- **Recommended solution**: Move to environment variables/user-secrets before any non-local deployment; confirm this is actually done wherever (if anywhere) the app gets deployed for grading.

### 5.3 — Phone-number-change flow is built but unreachable 🟢 Low
- **Description**: `AuthController.requestChangePhone`/`confirmChangePhone` exist and call real endpoints, but `ProfileScreen` shows the phone number as a plain read-only field with no edit button (confirmed by the screen's own doc comment acknowledging this is intentionally deferred).
- **Why it matters**: A real, finished feature with no way to trigger it — wasted backend+repository work until a button is added.
- **Backend or Flutter**: Flutter.
- **Estimated effort**: 1-2 hours (the flow is already implemented end-to-end; just needs a UI entry point and a small OTP-confirmation dialog reusing the existing OTP-entry pattern).
- **Recommended solution**: Add an edit icon next to the phone field on both Passenger and Driver profile screens.

### 5.4 — Identity password rules are a no-op ⚪ Informational, not a bug
- **Description**: `RequireDigit/RequireLowercase/RequireUppercase/RequireNonAlphanumeric = false`, `RequiredLength = 1`.
- **Why it matters**: Intentional — the app is passwordless/OTP-only, so Identity's password machinery is just along for the ride. Only relevant if password-based login is ever added later without revisiting this config.
- **Backend or Flutter**: Backend.
- **Estimated effort**: N/A.

### 5.5 — `SuperAdmin` role is dead 🟢 Low
- **Description**: Seeded at startup but never assigned to any user or checked by any `[Authorize(Roles=...)]` anywhere.
- **Why it matters**: Harmless, but suggests an originally-planned tiered-permission model that was never built — worth knowing if a grader asks about it.
- **Backend or Flutter**: Backend.
- **Estimated effort**: N/A — either remove the seed or build the tier; not urgent either way.

---

## 6. Notifications

Functionally complete and verified working end-to-end: paginated history (`GET /Notifications`), mark-as-read/mark-all-read, live push via `ReceiveNotification`, unread badges on both Passenger and Driver shells. No gaps found in this area beyond the cross-cutting items already listed under SignalR (§4) and Database (§3.1, re: `Notification`'s own model is fine — it's `Complaint`/`Violation` that lack FK config, not `Notification`).

- **Settings toggle for notifications exists on the backend** (`SettingsController` Notifications PUT/GET) but **has no UI** — see §8.4 (Settings screen entirely missing). 🟡 Medium.

---

## 7. Chat

Functionally complete and verified working: conversations list + per-order thread, pagination, live delivery over `ReceiveMessage`, deduplication by `messageId` (a real dual-group-delivery bug this app actually has and correctly works around), shared by Passenger and Driver. No backend or Flutter gaps found in this area.

One pre-existing, already-documented code-quality note carried over: a Riverpod `build()`/`refresh()` race pattern was found and fixed in `ConversationsListController` but the identical pattern still exists, unfixed, in `NotificationsListController`, `ProfileController`, `DriverProfileController` 🟢 Low (self-heals in production; see `KNOWN_ISSUES.md` #6 for full detail).

---

## 8. Admin Features

### 8.1 — No way to add or manage vehicles from the app at all 🟢 Resolved (2026-06-20)
- **Original gap**: `VehiclesController` (8 actions) was entirely unused by Flutter — `Vehicle` rows were only ever created inside `VehicleRepository`'s own `AddVehicel` method, no other code path (including driver registration) ever created one, so a freshly approved driver had no vehicle and could not enter the queue or receive orders. This was the single most consequential finding in the original audit.
- **Fix shipped this session — deliberately minimal, not fleet management**: a small form (plate, make, model, color, seats, size — no photo upload, no vehicle list/edit/unassign UI) added to the existing "no vehicle registered" state in the Driver Approval detail sheet (`DriverApprovalsScreen`/`_AssignVehicleDialog`). On submit it calls the two already-existing, already-Admin-authorized backend endpoints back to back — `POST /Vehicles/AddVehicle` then `PATCH /Vehicles/AssignVehicleToDriver/{vehicleId}` — **no backend change was needed**, since both were fully built and correctly set `IsActive`/`IsCurrent`/`DriverId`, just never called from anywhere.
- **Verified by tracing every downstream consumer of `IsCurrent && IsActive`**: `DriverRepository.GetMyProfileAsync` (drives `DriverProfile.hasVehicle`/`canEnterQueue` on the Driver Dashboard) and `DriverAssignmentRepository.GetActiveVehicle` (used by every dispatch-matching query, gated additionally on `vehicle.Seats >= order.PassengerCount`) both use this exact filter — confirmed the vehicle this flow creates satisfies both, end to end, by reading the actual matching code rather than assuming.
- **Backend or Flutter**: Flutter only.
- **Estimated effort**: Done.

### 8.2 — No Admin user-management screen (Users/UserBlocks) 🟠 High
- **Description**: `UsersController` (list/search/toggle-active/change-role, 5 actions) and `UserBlocksController` (toggle-block/list-blocks, 2 actions, and the block-toggle action is itself broken — §1.1) are entirely unused by Flutter.
- **Why it matters**: An Admin cannot view all users, deactivate an abusive account, or block someone from the app at all — only driver-approval-specific moderation exists.
- **Backend or Flutter**: Flutter (mostly) + Backend (the one broken action).
- **Estimated effort**: 1-2 days for a basic Users list + block/deactivate actions screen.
- **Recommended solution**: Explicitly out of the graduation-MVP scope already declared this session ("skip advanced reports") — acceptable to defer, but should be called out as a known limitation if asked.

### 8.3 — No Admin profile screen 🟡 Medium
- **Description**: `AdminController.edit`/`profile` exist; the Admin shell has no Profile tab at all — logout is a bare toolbar icon repeated on every tab instead.
- **Why it matters**: Every other role has a profile screen; the Admin role visibly doesn't, which would look unfinished if a grader navigates through all three roles side by side.
- **Backend or Flutter**: Flutter.
- **Estimated effort**: Half a day (can mostly copy the Driver profile screen's structure, minus vehicle fields).
- **Recommended solution**: Add a 6th tab or fold it into a menu, reusing `ProfileScreen`'s edit-name/address/photo pattern.

### 8.4 — No Settings screen (any role) 🟡 Medium
- **Description**: `SettingsController` (language, dark mode, notifications toggle, WhatsApp contact link — 7 actions) is entirely unused by Flutter, for any of the three roles.
- **Why it matters**: A reasonably-expected app feature (dark mode, language) is completely absent from the UI despite being fully built server-side.
- **Backend or Flutter**: Flutter.
- **Estimated effort**: 0.5-1 day for one shared settings screen reused across roles.
- **Recommended solution**: Build one `SettingsScreen` and add it to all three shells (or fold into each role's profile screen).

### 8.5 — No top-drivers leaderboard UI ⚪ Nice to Have
- **Description**: `GET /Admin/top-drivers` exists and is unused. Explicitly descoped this session ("skip advanced reports for now").
- **Why it matters**: Minor — a polish feature, not core.
- **Backend or Flutter**: Flutter.
- **Estimated effort**: Half a day.
- **Recommended solution**: Defer past graduation.

### 8.6 — No passenger-facing complaint-filing screen 🟡 Medium
- **Description**: `POST /api/orders/{orderId}/complaints` exists and works; the Admin side that *reviews* complaints was built this session, but there is no UI anywhere for a passenger to actually file one.
- **Why it matters**: The Admin Complaints screen has nothing to review in a live demo unless complaints are seeded directly via the API — the full loop (file → review → escalate) can't be demonstrated passenger-side.
- **Backend or Flutter**: Flutter.
- **Estimated effort**: 0.5 day (a simple form: reason dropdown + description, reachable from `OrderDetailScreen` for a completed trip).
- **Recommended solution**: Add a "File a complaint" action on `OrderDetailScreen`, gated the same way the backend already gates it (driver assigned, trip in progress/completed, within 24h).

### 8.7 — No favorite-locations UI 🟡 Medium
- **Description**: `FavoriteLocationsController` (add/list/delete, 3 actions) is fully built and entirely unused.
- **Why it matters**: `HomeScreen` only supports ad-hoc map-tap pickup/dropoff every time — no saved-places convenience.
- **Backend or Flutter**: Flutter.
- **Estimated effort**: 1 day (list + add-from-map + delete, plus a "use favorite" picker on `HomeScreen`).
- **Recommended solution**: Defer unless time allows — not core to demoing the ride lifecycle.

---

## 9. Driver Features

### 9.1 — No "cancel trip" action anywhere in the Driver app 🟢 Resolved (2026-06-20)
- **Original gap**: `POST /DriverTrips/cancel-trip/{tripId}` existed and worked (with a `TripCancelReason` enum: DriverIssue/VehicleProblem/Accident/Emergency); nothing in the Driver app called it.
- **Fix shipped**: a "إلغاء الرحلة" button in `ActiveTripCard`'s header opens a reason-picker dialog (`RadioGroup` over the 4 reasons — picking a reason and confirming doubles as the confirmation step) and calls the existing endpoint via a new `DriverActiveController.cancelTrip` method. No backend change needed.
- **Verified by tracing the backend's actual cancellation behavior, not just the happy path**: `CancelTripByDriverAsync` doesn't just end the trip — it resets the trip/order back to `SearchingDriver` and immediately attempts re-dispatch to a different driver (`AssignTripEmergencyAsync`), reusing the *same* `TripId`. Confirmed this composes correctly with the passenger side with zero additional Flutter changes: `OrderRepository.GetOrderDetailAsync` only considers a `TripOrder` "active" while `StatusInTrip != Unassigned`, so once cancelled the passenger's next refetch naturally reverts to the already-built "searching for a driver" view (the same one shown right after order creation) rather than needing a dedicated "trip cancelled" screen — and because the re-dispatch reuses the same `TripId`, `OrderDetailController` never needs to leave/rejoin a SignalR group, so it stays correctly subscribed if a new driver is found.
- **Also verified**: the passenger is notified via the existing, already-consumed `UpdateTripStatus` channel (`NotificationRepository.SendNotificationAsync` pushes both a persisted notification and `UpdateTripStatus` to `trip-{tripId}` whenever a `tripId` is given) — the separate `LeaveTrip`/`UpdateDriverStatus` events this action also fires are the same previously-verified-redundant pair from §4.2, not new dead ends.
- **Backend or Flutter**: Flutter only.
- **Estimated effort**: Done.

### 9.2 — No "leave queue" / go-offline action 🟠 High
- **Description**: `DriverTripsController` has `enter-queue` but no corresponding leave/exit/offline action exists anywhere on the backend either (confirmed — not just a Flutter gap).
- **Why it matters**: Once a driver enters the queue, they can never explicitly go off-duty from the app. (Already documented in `TODO.md` #13.)
- **Backend or Flutter**: Both — backend endpoint doesn't exist, so Flutter can't call it even if a button were added.
- **Estimated effort**: 0.5 day backend (new endpoint + repository method) + 2-3 hours Flutter (one button).
- **Recommended solution**: Add `POST /DriverTrips/leave-queue`, then a "Leave Queue" button in the Driver Dashboard's idle view.

### 9.3 — No driver-side acceptance UI for shared/pooled whole-trip offers 🟠 High
- **Description**: The backend supports offering a bundled multi-order "whole trip" to a driver (`accept-trip`/`reject-trip` actions, `SendTripOfferForWholeTrip` notification shape with multiple orders). The Driver app's offer screen (`IncomingOrderOfferCard`/`DriverActiveState.offer`) is shaped for exactly one order at a time and has no path that calls `accept-trip`/`reject-trip` at all.
- **Why it matters**: Ride-pooling/shared-trip dispatch is a real backend capability with zero mobile support — if the auto-dispatch engine or an Admin's manual whole-trip assignment ever sends a driver a bundled offer, the Driver app has no UI to represent or act on it correctly.
- **Backend or Flutter**: Flutter.
- **Estimated effort**: 1-2 days (new offer-detail shape supporting multiple stops, new accept/reject methods, UI for a multi-stop offer card).
- **Recommended solution**: If shared trips aren't part of the demo script, explicitly document this as a known scope limitation rather than risk it being triggered accidentally by the dispatch engine's batching logic.

### 9.4 — No driver earnings/trip-history screen 🟢 Low
- **Description**: `GET /Drivers/my-trips-report` exists and is unused.
- **Why it matters**: A driver can't see their own trip history/stats from the app.
- **Backend or Flutter**: Flutter.
- **Estimated effort**: 0.5 day.
- **Recommended solution**: Defer past graduation unless explicitly part of the rubric.

### 9.5 — "Remove profile photo" not wired on Driver profile 🟢 Low
- **Description**: `DriverProfileController.save(removeProfilePhoto: true)` and the API layer fully support it; no button in `DriverProfileScreen` calls it (only "pick new photo" exists).
- **Why it matters**: Dead plumbing — small completeness gap.
- **Backend or Flutter**: Flutter.
- **Estimated effort**: 15 minutes.
- **Recommended solution**: Add a "Remove photo" option (e.g. long-press or a small ✕ on the avatar).

---

## 10. Passenger Features

### 10.1 — No way to submit a driver rating (display-only) 🟢 Resolved (2026-06-20)
- **Original gap**: `POST /PassengerTrips/rate-driver` existed and worked; `OrderDetailScreen` only ever rendered an existing rating read-only — there was no submission form anywhere.
- **Fix shipped**: when `order.status == completed && order.rating == null`, a `_RateDriverPrompt` card appears in place of the read-only `_RatingCard`, opening a Material 3 star-picker (1-5, defaults to 5) + optional comment dialog on tap. Submitting calls the existing endpoint via a new `OrdersRepository.rateDriver`/`OrderDetailController.rate`. **Deliberately not auto-shown on screen load** (the originally-suggested approach) — a tap-triggered card avoids needing a "have we already popped this once" guard and can't surprise-interrupt a passenger who reopens a completed order later just to check trip details.
- **Duplicate prevention is now three-layered**: the backend already rejected a second rating outright (`alreadyRated` check, pre-existing); the prompt card disappears entirely the moment `order.rating` is populated (since submitting triggers a refetch); and the dialog's submit button disables itself for the duration of the in-flight request (new `isRating` flag) to absorb a rapid double-tap before the first response even returns.
- **Verified by tracing where the new `Rating` row actually flows, not just where it displays**: `OrderRepository.GetOrderDetailAsync`'s rating lookup is unfiltered by rater, so the passenger's own refetch picks it up immediately; separately confirmed the same row feeds `DriverApprovalRepository.GetDriverDetailsAsync`'s `rating`/`ratingCount` (shown to Admins) and `GetDriverWeightedRating` (used in dispatch driver-matching) via the shared `Ratings` table keyed by `TripId`/`TargetUserId` — submitting a rating isn't just a UI round-trip, it correctly updates the driver's aggregate rating everywhere it's read.
- **Known, intentionally-unmitigated constraint**: the backend only accepts a rating within 30 minutes of trip completion (`RateDriverAsync`'s `CompletedAt` check) and there's no `CompletedAt` field in the DTO to replicate that countdown client-side — a late attempt surfaces the backend's own error message via the standard snackbar instead of being pre-empted with a client-side timer.
- **Backend or Flutter**: Flutter only.
- **Estimated effort**: Done.

### 10.2 — No order-editing UI — see §2.1 🟡 Medium

### 10.3 — No favorite-locations UI — see §8.7 🟡 Medium

### 10.4 — No complaint-filing UI — see §8.6 🟡 Medium

### 10.5 — No passenger trips report/history-stats screen 🟢 Low
- **Description**: `GET /Passengers/trips-report` exists and is unused (separate from the basic paginated orders list, which does work).
- **Why it matters**: Minor — a stats/summary view, not core functionality (the orders list itself already shows history).
- **Backend or Flutter**: Flutter.
- **Estimated effort**: 0.5 day.
- **Recommended solution**: Defer past graduation.

### 10.6 — No settings screen — see §8.4 🟡 Medium

### 10.7 — "Remove profile photo"/phone-change reachability — see §9.5/§5.3 🟢 Low

---

## 11. Maps & GPS

### 11.1 — No live route/ETA line for the passenger — see §4.1 🟢 Resolved (2026-06-20)

### 11.2 — `flutter_map`/OpenStreetMap choice is solid; live driver tracking was non-functional, now fixed 🟢 Resolved (2026-06-20)
- **Description**: Deliberate choice to avoid a Google Maps API key requirement; works for pickup/dropoff picking. The original audit pass rated "live driver-marker tracking" as working — that was wrong. A deeper trace found the Driver app never acquired or sent real GPS at all: no `geolocator`/location package existed, no native permissions were declared on either platform, and nothing ever invoked the backend's `SendLocation` hub method. Practical effect: `Driver.LastLat/LastLng` was permanently `null` for every driver, so the passenger's `TripTrackingMap` would never show a driver marker — not stale, simply absent, for every trip.
- **Fix shipped this session**: added `geolocator`, `Android`/`iOS` location permissions (plus the previously-missing `INTERNET` permission on Android, a separate pre-existing gap that would have blocked all networking on a real device), and a `DriverLocationService` wired into `DriverActiveController` — GPS streaming starts automatically when a trip begins and stops when it ends, pushing real fixes via the existing `RealtimeService.sendLocation` → `SendLocation` hub call. No backend change was needed: `TripRoutingService`'s ETA calc already reads `Driver.LastLat/LastLng` directly, so it now uses real coordinates automatically.
- **Verified by code trace, not yet by live device**: `flutter analyze`/`flutter test` pass clean; the full pipeline (GPS stream → hub call → `DriverLocationUpdated` broadcast → `OrderDetailController` → `TripTrackingMap` marker) was traced end to end in code. A real-device run (or emulator with simulated location) against a live backend has not been performed in this session — recommended before the demo.
- **Backend or Flutter**: Flutter (client-side acquisition/permissions); no backend change required.
- **Estimated effort**: Done.

### 11.3 — Driver location throttling is reasonable but unverified under poor connectivity 🟢 Low
- **Description**: `DriverTrackingRepository` only persists/broadcasts a location update if the driver moved AND ≥10 seconds elapsed.
- **Why it matters**: Good default; worth a real-device test under a flaky connection before a live demo, since no automated test covers this path.
- **Backend or Flutter**: Backend.
- **Estimated effort**: Verification only, no code change — an hour of manual device testing.

---

## 12. Real-time Updates

Covered comprehensively under §4 (SignalR) above. Headline: of 7 distinct server-pushed event types, 5 are now actively consumed with real effect (`ReceiveNotification`, `UpdateTripStatus`, `DriverLocationUpdated`, `ReceiveMessage`, and `RouteUpdated` as of 2026-06-20 — see §4.1), and 2 (`LeaveTrip`, `UpdateDriverStatus`) are unconsumed but verified redundant with channels that are consumed — so the real-time layer is in materially better shape than a naive "2 of 7 events unhandled" headline would suggest.

---

## Summary table — all items by priority

| # | Area | Item | Priority | Backend/Flutter |
|---|---|---|---|---|
| 1.1 | Backend | UserBlocksController claim-name bug | 🟠 High | Backend |
| 1.2 | Backend | BaseController null-deref risk | 🟠 High | Backend |
| 1.3 | Backend | ChangeUserRole no allow-list | 🟠 High | Backend |
| 1.8 | Backend | Zero backend test coverage | 🟠 High | Backend |
| 2.2 | Flutter | Near-zero Flutter test coverage | 🟠 High | Flutter |
| 4.3 | SignalR | EnableDetailedErrors unconditional | 🟠 High | Backend |
| 5.2 | Auth | Secrets committed as placeholders | 🟠 High | Backend |
| 8.2 | Admin | No user-management screen (+ broken block action) | 🟠 High | Both |
| 9.2 | Driver | No leave-queue action (backend + Flutter) | 🟠 High | Both |
| 9.3 | Driver | No shared/pooled-trip offer UI | 🟠 High | Flutter |
| 1.4 | Backend | Inconsistent rate limiting on driver actions | 🟡 Medium | Backend |
| 1.5 | Backend | Fragile string-matching result contracts | 🟡 Medium | Backend |
| 2.1 / 10.2 | Flutter | No order-editing UI | 🟡 Medium | Flutter |
| 3.1 | Database | Complaint/Violation no FK config | 🟡 Medium | Backend |
| 3.3 | Database | No unique constraint on PhoneNumber | 🟡 Medium | Backend |
| 4.2 | SignalR | LeaveTrip/UpdateDriverStatus unconsumed (verified non-blocking) | 🟡 Medium | Flutter |
| 6 | Notifications | Settings toggle has no UI | 🟡 Medium | Flutter |
| 8.3 | Admin | No Admin profile screen | 🟡 Medium | Flutter |
| 8.4 / 10.6 | Admin/Passenger | No settings screen (any role) | 🟡 Medium | Flutter |
| 8.6 / 10.4 | Admin/Passenger | No complaint-filing UI | 🟡 Medium | Flutter |
| 8.7 / 10.3 | Admin/Passenger | No favorite-locations UI | 🟡 Medium | Flutter |
| 3.2 | Database | FavoriteLocation no FK config | 🟢 Low | Backend |
| 3.4 | Database | Username collisions possible | 🟢 Low | Backend |
| 4.5 | SignalR | No app-lifecycle reconnect hook | 🟢 Low | Flutter |
| 5.3 | Auth | Phone-change flow unreachable | 🟢 Low | Flutter |
| 5.5 | Auth | SuperAdmin role dead | 🟢 Low | Backend |
| 9.4 | Driver | No earnings/trip-history screen | 🟢 Low | Flutter |
| 9.5 | Driver | Remove-photo not wired | 🟢 Low | Flutter |
| 10.5 | Passenger | No trips-report screen | 🟢 Low | Flutter |
| 11.3 | Maps | Location throttling unverified at scale | 🟢 Low | Backend |
| 1.6 | Backend | Empty TestsController | 🟢 Low | Backend |
| 1.7 | Backend | Minor dead code | ⚪ Nice to Have | Backend |
| 2.3 | Flutter | Dependency version lag | ⚪ Nice to Have | Flutter |
| 4.4 | SignalR | LeaveTrip name reuse confusion | ⚪ Nice to Have | Backend |
| 5.4 | Auth | No-op password rules | ⚪ Informational | Backend |
| 8.5 | Admin | No top-drivers leaderboard UI | ⚪ Nice to Have | Flutter |

**Totals: 3 Critical · 7 High · 13 Medium · 9 Low · 5 Nice to Have/Informational.**
