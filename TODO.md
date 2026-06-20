# TODO — TaxiApp

Generated from a code-level gap analysis on 2026-06-20. Ordered by priority within each section. This is a backlog, not a bug list — see `KNOWN_ISSUES.md` for defects in code that already exists.

---

## P0 — Blocking / functionally broken (fix before relying on these features)

1. **Fix `UserBlocksController.ToggleUserBlock`** — currently unusable for every caller (always returns 401). See `KNOWN_ISSUES.md` §Bugs #1.
2. **Decide and build the missing rating-submission endpoint** — `Notification.RateTrip` exists to prompt a rating, and the mobile app displays a rating once present, but no `POST` rating-creation endpoint was found on the backend, and there is no submission UI on mobile. Without this, the rating feature is display-only and can never actually be populated through normal use.
3. **Null-check the access-check gate** (`BaseController.CheckUserAccessAsync`) — a stale/deleted-from-DB JWT currently 500s instead of 401/403 across most of the API surface. See `KNOWN_ISSUES.md` §Bugs #2.

## P1 — Missing features (backend ready, mobile client work not started)

4. ~~**In-trip chat UI**~~ — **Done (2026-06-20).** `lib/features/chat/` built: conversations list + per-order thread, paginated, live over a new `RealtimeService.messages` stream (`ReceiveMessage`), deduped by `messageId`. Reachable from a Messages tab on both the Passenger and Driver shells, plus a quick-access icon on `OrderDetailScreen`/the Driver's active-trip stop card. Driver/Admin apps below are still not built — chat is wired into whichever of those shells already exists.
5. **Order-editing UI** — `PUT /api/Orders/{id}` (`EditOrder`) exists and works; no corresponding call exists anywhere in the Flutter app.
6. **Favorite-locations UI** — `FavoriteLocationsController` (add/list/delete) is fully built; `HomeScreen` only supports ad-hoc map-tap pickup/dropoff, no saved-places concept.
7. **Complaints UI** — backend complaint→violation escalation pipeline exists; no Flutter screen to file a complaint.
8. **Phone-number-change flow UI** — `requestChangePhone`/`confirmChangePhone` are fully implemented in the mobile repository/API layer already, just never triggered from any screen; `ProfileScreen` shows the phone number read-only with a comment noting this is intentionally deferred.
9. **Settings screen** — backend `SettingsController` supports language/dark-mode/notification-toggle; mobile has no settings screen exposing any of it (only profile-edit + logout exist today).
10. **"Remove profile photo" action** — `removeProfilePhoto` is fully plumbed through `profile_api.dart` → `profile_repository_impl.dart` → `profile_controller.dart`, but no button/action in `profile_screen.dart` calls it. Either wire it up or remove the dead plumbing.

## P2 — Whole surfaces not started

11. ~~**Driver mobile app**~~ — **Done (2026-06-20), MVP scope.** `lib/features/driver/` + `DriverShellScreen`: Dashboard/Enter-Queue/Incoming-Offer/Accept/Reject/Active-Trip/Arrived/Pickup/Dropoff (one adaptive screen switching on dispatch state), Notifications, Profile, and now Messages (chat). Required adding 2 backend GET endpoints (`/Drivers/profile`, `/DriverTrips/active`) that didn't exist before — see `ARCHITECTURE.md`. Earnings/trip-history UI still not built (`GET /Drivers/my-trips-report` exists, unused by mobile).
12. ~~**Admin mobile app**~~ — **Done (2026-06-20), graduation-demo MVP scope.** `lib/features/admin/` + `AdminShellScreen` (5 tabs): Orders dashboard, Trips dashboard, Driver approvals (list + approve/reject + detail sheet), Manual assignment (assign-to-order/assign-to-trip + Auto/Manual dispatch toggle), Complaints management (complaints + violations, status updates, complaint→violation escalation). Required 3 backend changes — see `ARCHITECTURE.md`: fixed `AdminController.GetTrips`'s missing `[FromQuery]` binding, added `GET /DriverAssignmentManual/assignable-drivers`, and enriched `GET /Complaints/all` with resolved sender/against display names (new `ComplaintDto`/`ViolationDto`). Still not built (explicitly out of this pass — "skip advanced reports"): vehicle fleet management, user blocking UI, top-drivers leaderboard, an Admin profile screen (logout is a toolbar action instead), and document review for driver approvals (the backend has no document fields to review at all — see `KNOWN_ISSUES.md` #16).
13. **"Leave queue" / go-offline endpoint** — `DriverTripsController` has `enter-queue` but no corresponding leave/offline action anywhere in the controller set. Confirmed still missing after building the Driver app above — its Dashboard can only enter the queue, never leave it. Needed before a real Driver app can let a driver go off-duty.

## P3 — Hardening / production-readiness (see `KNOWN_ISSUES.md` for the full list; these are the actionable follow-ups)

14. Lock down CORS (`Program.cs`) — replace `SetIsOriginAllowed(_ => true)` + `AllowCredentials()` with an explicit allowed-origin list.
15. Gate Swagger UI behind `IsDevelopment()` — currently exposed unconditionally in production.
16. Gate `SignalR.EnableDetailedErrors` behind `IsDevelopment()`.
17. Move `JWT:Secret`/Twilio credentials out of `appsettings.json` placeholders into environment/user-secrets/Key Vault for real deployments, and confirm this is actually done wherever the app is deployed.
18. Add an explicit allow-list validation to `UsersController.ChangeUserRole` (currently accepts any free-text string as a role name).
19. Add the missing `IsUserBlocked` check to `FavoriteLocationsController`, and route `NotificationsController`/`SettingsController` through `BaseController.CheckUserAccessAsync` for consistency with every other authenticated controller.
20. Add a unique DB constraint on `ApplicationUser.PhoneNumber` (currently enforced only in application code, not the schema).
21. Add `NSPhotoLibraryUsageDescription` (and `NSCameraUsageDescription` if camera capture is ever added) to `ios/Runner/Info.plist` — required before this app can ship to iOS, since `image_picker` is already used in `ProfileScreen`.
22. Add automated test coverage — backend has none of its own called out in this pass (worth a dedicated audit); mobile has only the default Flutter template test (`test/widget_test.dart`), zero coverage of repositories/controllers/the `ApiException` mapping logic, zero widget tests across 18 real screens.
23. Replace the fragile Arabic-string-matching control flow (`login_message_classifier.dart` on mobile; `result.Contains("success")`-style checks in `DriverTripsController`/`PassengerTripsController` on the backend) with typed result codes/enums — any backend copy change currently risks silently breaking client-side branching.
24. Bump pinned mobile dependencies that are a major version behind (`flutter_riverpod` 2.x→3.x, `go_router` 14.x→17.x, `flutter_secure_storage` 9.x→10.x, `flutter_lints` 4.x→6.x) — not urgent (current pins are stable, `flutter analyze` is clean), but plan for Riverpod 3's breaking changes before it becomes a forced upgrade.

## P4 — Smaller cleanups (low effort, no urgency)

25. Delete the empty `TestsController.cs` scaffold — zero endpoints, just route-table/Swagger clutter.
26. Remove the duplicate `using TaxiApp.Backend.Core.Settings;` line in `Program.cs`.
27. Remove the unused `ApplicationDbContext` injection from `NotificationsController`.
28. Remove unused `using Azure.Core;`/`using Twilio.Http;` from `AuthRepository.cs`.
29. Fix the `AddVehicel` → `AddVehicle` method-name typo (`IVehicleRepository`/`VehicleRepository`).
30. Add the missing `[EnableRateLimiting("DriverActionsPolicy")]` to `accept-trip`/`reject-trip` for consistency with `accept-order`/`reject-order`.
31. Replace the `Console.WriteLine` in `NotificationHub.cs` with the `ILogger` pattern used everywhere else in the codebase.
32. Switch OTP generation from `new Random()` to `RandomNumberGenerator` for consistency with the refresh-token generator (low severity given rate limiting, but inconsistent).
