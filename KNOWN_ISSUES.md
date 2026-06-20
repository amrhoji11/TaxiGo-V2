# KNOWN_ISSUES — TaxiApp

Generated from a direct code audit on 2026-06-20. Every item below was verified by reading the cited file/line directly (not inferred). File paths are relative to each repo's root (`TaxiApp.Backend/` or `TaxiApp.Mobile/`).

---

## Bugs (confirmed, reproducible from code alone)

1. **`UserBlocksController.ToggleUserBlock` is permanently broken.** `TaxiApp.Backend/Controllers/UserBlocksController.cs:27` reads the acting admin's id via `User.FindFirstValue("UserId")`, but `JwtService.GenerateToken` (`TaxiApp.Backend.Core/JwtService.cs:26-32`) never issues a claim literally named `"UserId"` — only `ClaimTypes.NameIdentifier` (which is a different claim type string). `officeId` is therefore always `null`, so line 28's `if (officeId == null) return Unauthorized();` fires on every single call. **Impact**: `PATCH /api/UserBlocks/{userId}/ToggleUserBlock` cannot succeed for any Admin, ever, as currently written. Fix: read `User.FindFirstValue(ClaimTypes.NameIdentifier)` instead.

2. **`BaseController.CheckUserAccessAsync` will throw `NullReferenceException` instead of returning 401/403.** `TaxiApp.Backend/Controllers/BaseController.cs:27-29`:
   ```csharp
   var user = await userRepository.GetUserByIdAsync(userId);
   if (user.IsDeleted)   // no null check
   ```
   `GetUserByIdAsync` can return `null` (e.g. a syntactically valid JWT for a user row that no longer exists). This gate is used by `Orders`, `Passengers`, `Drivers`, `DriverTrips`, `Vehicles`, `PassengerTrips`, `Messages`, and `Complaints` controllers — a fairly central code path. **Impact**: an edge-case request crashes with a 500 instead of a clean auth rejection. Fix: null-check before dereferencing.

3. **`UsersController.ChangeUserRole` accepts arbitrary free-text role names.** `TaxiApp.Backend/Controllers/UsersController.cs:69-79` → `UserRepository.ChangeUserRole` performs no allow-list validation against the four seeded roles (`SuperAdmin`/`Admin`/`Driver`/`Passenger`). **Impact**: an Admin (or a request crafted to hit this endpoint) can assign any string as a user's role, including typos or roles that don't exist, silently corrupting authorization state for that user.

4. ~~**`RouteUpdated` SignalR event never reaches the passenger.**~~ **Fixed 2026-06-20.** `TripRoutingService.RecalculateTripAsync` now also broadcasts a passenger-safe `{ polyline, totalMinutes }` subset to `trip-{tripId}` (full per-stop payload, with other passengers' names on shared trips, stays driver-only). A separate bug found in the same pass: `BuildSharedTripRoute`'s planning loop never computed a dropoff leg for single-order trips (it dropped the order from consideration right after the pickup leg) and the post-pickup recalculation was skipped for non-shared trips — both fixed. Flutter: `RealtimeService.routeUpdates` + a shared `TripRoute` parser (real polyline when available, straight-line fallback through the stop sequence otherwise) + `PolylineLayer` rendering on `TripTrackingMap`, used by both Driver and Passenger screens.

5. **Missing iOS permission string for photo picker.** `ios/Runner/Info.plist` (mobile repo) has no `NSPhotoLibraryUsageDescription` key, but `ProfileScreen` (`lib/features/profile/presentation/screens/profile_screen.dart:54`) calls `ImagePicker().pickImage(source: ImageSource.gallery)`. **Impact**: this will fail App Store review and/or crash or silently fail at the permission prompt on a real iOS device/simulator. Not yet hit because verified testing has been Android-emulator/web only.

6. **Mobile: latent Riverpod race in the `build()` → `unawaited(refresh())` pattern.** Confirmed via a real widget test (`test/chat_screens_test.dart`, written for the Chat feature) that calling `refresh()` via `unawaited(...)` directly inside a `Notifier.build()` throws `StateError: Tried to read the state of an uninitialized provider` if `refresh()`'s first synchronous statement reads `state` (e.g. `final current = state;`/`if (state is! ...)`) — that read executes synchronously inside `build()`'s own call stack, before Riverpod finishes registering the value `build()` is about to return. In production this is silently swallowed (an async function's synchronous-prefix throw becomes a rejected, never-awaited Future, so it surfaces only as an unhandled-exception log line, not a crash) and self-heals on the next real `refresh()` call — which is why it was never noticed. **Confirmed present in**: `NotificationsListController.build()`/`.refresh()` (`lib/features/notifications/presentation/providers/notifications_list_controller.dart`), `ProfileController.build()`/`.refresh()`, `DriverProfileController.build()`/`.refresh()` (same shape: `state = const ...Loading();` as `refresh()`'s first line). **Fixed in**: `ConversationsListController` (Chat feature session) via `Future.microtask(refresh)` instead of `unawaited(refresh())`. The Admin feature's controllers (`OrdersDashboardController` and friends) were written with this fix already in mind from the start. The other three pre-existing controllers were left as-is (out of scope for the tasks that surfaced this) — apply the same `Future.microtask` fix if they're ever touched, or if this pattern starts being asserted against in tests.

---

## Potential risks

7. **CORS policy combines wildcard origin with credentials.** `Program.cs:150-160`:
   ```csharp
   policy.SetIsOriginAllowed(_ => true).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
   ```
   This is a textbook CSRF/credential-leak misconfiguration pattern. Currently the app is a JWT-bearer mobile API (not cookie-based), which limits real exploitability today, but it should not ship to production as-is — tighten to an explicit allowed-origin list before any browser-based client (e.g. a future Admin web surface) is added.

8. **Swagger UI is exposed unconditionally in production.** `Program.cs:264-265` calls `app.UseSwagger()`/`app.UseSwaggerUI()` outside the `IsDevelopment()` check (only `MapOpenApi()` at line 261 is dev-gated). **Impact**: the full API schema and a live "try it out" UI are reachable by anyone who finds the route in production.

9. **SignalR detailed errors are unconditional.** `Program.cs:138` sets `EnableDetailedErrors = true` with no environment gate — can leak internal exception details (stack traces, internal type names) to any connected hub client in production.

10. **Secrets committed as placeholders in `appsettings.json`.** `JWT:Secret`, `Twilio:AccountSid/AuthToken`, and `GoogleMaps:ApiKey` are placeholder literals in source control (`appsettings.json:14,33-35`). Low risk as long as every real deployment overrides them via environment variables/user-secrets/Key Vault — but this must be actively confirmed, since a forgotten override means a publicly-known, guessable JWT signing key (tokens become forgeable).

11. **OTP codes use `new Random()`, not a CSPRNG.** `AuthRepository.cs:69,178,496`. Refresh tokens correctly use `RandomNumberGenerator` (`AuthRepository.cs:588-594`) — the inconsistency suggests this wasn't a deliberate choice. Low severity in isolation (6-digit codes are rate-limited and short-lived), but `Random()`-generated values are theoretically more predictable.

12. **No unique DB constraint on `ApplicationUser.PhoneNumber`.** Phone is the sole login identifier, but uniqueness is only checked in application code at registration time, not enforced by a unique index at the schema level — a race condition (two near-simultaneous registrations) could create two accounts with the same phone number.

13. **Username collisions are plausible.** Registration sets `UserName = FirstName` only (`AuthRepository.cs:118,224`), not a phone-derived or otherwise-unique value. Two passengers registering with the same first name around the same time risk a generic ASP.NET Identity "username taken" failure that has nothing to do with their actual identity.

14. **`Complaint`/`Violation` have zero foreign-key/navigation properties.** `SenderId`/`AgainstUserId`/`DriverId`/`OrderId`/`TripId` on these two models are bare string/int columns with no DB-enforced referential integrity anywhere in the entire complaints/violations subsystem — orphaned or misspelled IDs are possible and undetectable by the database.

15. **`FavoriteLocation.UserId` has no explicit FK configuration.** Unlike every other relationship in `ApplicationDbContext.OnModelCreating`, this one relies on EF's implicit convention via the `User` navigation property rather than explicit configuration — works today, but is an inconsistency that makes the model harder to reason about and easier to break with a future refactor.

16. **No document/photo fields exist anywhere for driver-approval review.** Confirmed while building the Admin app's Driver Approvals screen: `DriverApprovalRepository.GetDriverDetailsAsync` (`DriverApprovalRepository.cs:116-168`) returns only name/email/phone/status/vehicle-model-and-plate/trip-stats/rating — there is no license image, ID photo, or vehicle-registration-document field on `Driver`, `Vehicle`, or `DriverApproval` at all, only `Driver.ProfilePhotoUrl`. **Impact**: an office admin approving/rejecting a driver registration today has no way to see any submitted identity/vehicle documents through this API, because there's nothing in the schema to serve — this is a backend/data-model gap, not something the Admin mobile screen omitted. Needed before document verification can be a real part of the approval flow.

---

## Code quality concerns

17. **Layering violation: `NotificationsController` injects `ApplicationDbContext` directly** (`NotificationsController.cs:17,19,22`) instead of going through `INotificationRepository` only — and the injected field is never actually used in any action. Dead code plus the one crack in an otherwise consistently enforced Core/Infrastructure/Api boundary.

18. **Inconsistent access-check coverage.** `FavoriteLocationsController` duplicates the deleted/active checks inline (`FavoriteLocationsController.cs:35-46,63-73,90-100`) but never calls `IsUserBlocked` — a blocked passenger can still manage favorite locations, unlike every other passenger-facing action. `NotificationsController` and `SettingsController` skip `BaseController`/`CheckUserAccessAsync` entirely — a blocked or deactivated user can still read notifications and change settings.

19. **Fragile string-matching control flow on both sides of the stack.**
    - Backend: several `DriverTripsController`/`PassengerTripsController` actions branch on substring-matching a repository's free-text return string (e.g. `result.Contains("success")` at `DriverTripsController.cs:188`, `!result.Contains("Driver")` at line 61, `!result.Contains("offer")` at line 99) rather than typed result objects/enums. The same shape exists on `DriverAssignmentManualController`'s `manual-assign-order`/`manual-assign-trip` actions and `ComplaintsController.Create`/`UpdateStatusAsync`/`ResolveViolationAsync` — all of these always return HTTP 200 regardless of outcome, with success/failure distinguishable only by the bare string body (`"Manually assigned"` vs `"Driver not available"` vs `"Order not found"`, etc). The Admin mobile screens built against `DriverAssignmentManualController` surface that raw string back to the admin verbatim rather than trying to infer success from the HTTP status, since the status can't actually tell them.
    - Mobile: `login_message_classifier.dart:14-36` classifies login outcomes by string-matching the exact Arabic message text the backend returns. The file's own doc comment acknowledges this is "inherently fragile."
    Any backend copy change for these specific messages silently breaks behavior on both ends without a compiler error anywhere.

20. **Inconsistent request-DTO validation.** `SendMessageDto`, `Message`, `Notification`, `CreateOrderDto` use `[Required]`/`[MaxLength]`/`[Range]` DataAnnotations (so `[ApiController]` rejects bad input automatically with a 400); but `RateDriverRequest.Stars`, `AssignDriverDto.DriverId`, `CancelTripDto`, `AddFavoriteLocationDto`, and most other request DTOs have none — their validation is hand-rolled inside repository methods instead, so malformed requests reach business logic before being rejected, and the error response shape differs between the two styles.

21. **`UserRepository.GetAllUsersAsync` loads the entire `Users` table into memory** before paginating in C# (`UserRepository.cs:60` — `context.Users.AsNoTracking().ToListAsync()` then `Skip/Take`), inconsistent with `SearchUsersAsync` in the same class and the generic `Repository<T>.GetAll`, both of which paginate at the SQL level. Will not scale as the user table grows.

22. **`MessageRepository.GetUserConversationsAsync` (`MessageRepository.cs:177-229`) pulls all of a user's order-scoped messages into memory** with no upper bound before grouping into a conversation list. Fine at current scale; will degrade for a heavy chat user since there's no pagination on the conversations list itself (only within a single opened conversation — and the mobile client mirrors this faithfully: `ConversationsListController`/`GET /Messages/conversations` is intentionally unpaginated on both ends).

23. **Minor dead code / sloppiness**: duplicate `using TaxiApp.Backend.Core.Settings;` (`Program.cs:14-15`); unused `using Azure.Core;`/`using Twilio.Http;` in `AuthRepository.cs:1,23`; `Console.WriteLine` debug leftover in `NotificationHub.cs:39` (everywhere else uses injected `ILogger`); `AddVehicel` method-name typo in `IVehicleRepository`/`VehicleRepository.cs:24`; empty `TestsController.cs` scaffold with zero endpoints.

24. **Inconsistent rate-limit coverage.** `DriverTripsController.cs:70,89` (`accept-trip`/`reject-trip`) lack the `[EnableRateLimiting("DriverActionsPolicy")]` attribute that the otherwise-equivalent `accept-order`/`reject-order` actions have (lines 31-32, 50-51).

25. **Mobile: dead plumbing for "remove profile photo."** Fully wired through `profile_api.dart` → `profile_repository_impl.dart` → `profile_controller.dart`, but no UI action calls it (`profile_screen.dart` only exposes "pick new photo").

26. **Mobile: several force-unwraps (`!`) without a guarding check**, e.g. `app_router.dart:60,64,92` (`state.extra!`, `int.parse(...)!`). Most are deliberate "crash loudly on programmer error" choices (one, `login_response_model.dart:37`, is explicitly commented as such) rather than accidental, but a single typo'd route wiring would currently crash with no graceful fallback rather than a handled error.

27. **No automated test coverage of substance.** Mobile: until the Chat feature's `test/chat_screens_test.dart`, `test/widget_test.dart` contained exactly one trivial test (asserts `SplashScreen` shows a spinner) — still effectively zero coverage of repositories, controllers, validators, or the `ApiException` mapping logic across the app's screens (the chat test covers 2 of them; the Admin MVP added no new tests of its own). Backend: not exhaustively audited in this pass, but no test project was identified alongside the 3 main projects in the solution — worth a dedicated follow-up to confirm.

---

## Performance issues

28. **`UserRepository.GetAllUsersAsync`** — see Code quality #21. Unbounded full-table load before in-memory pagination.

29. **`MessageRepository.GetUserConversationsAsync`** — see Code quality #22. Unbounded full-history load before in-memory grouping.

30. **`AdminRepository.SoftDeleteDriverAsync`/`RestoreDriverAsync`/`SoftDeletePassengerAsync`/`RestorePassengerAsync`** each touch two related entities (User + Driver/Passenger) without an explicit transaction — works correctly today because both updates ride the same `DbContext`'s single `SaveChangesAsync()` call, but it's implicit rather than explicit, and would silently stop being atomic if either method were ever refactored to call `SaveChangesAsync()` twice.

31. **`ComplaintRepository.UpdateStatusAsync`** calls `SaveChangesAsync()` multiple times within one logical operation (`ComplaintRepository.cs:151,179,186`) rather than once at the end — not incorrect, but unnecessarily chatty round-trips to the database.

32. **`ComplaintRepository.GetAllComplaintsAsync`/`GetAllViolationsAsync` load their entire tables into memory with no pagination at all** — same unbounded-load shape as #29 (`MessageRepository`), confirmed while wiring the Admin Complaints screen against both endpoints. Acceptable at current scale (complaints/violations are low-volume relative to orders/messages), and the Admin mobile client deliberately doesn't paginate either, matching the backend's own assumption — but flag this if complaint volume ever grows enough to matter.

33. No reconnect/lifecycle handling beyond signalr_netcore's built-in `withAutomaticReconnect()` on mobile (`RealtimeService` has no app-foreground/background hook) — acceptable under the app's "realtime is a progressive enhancement" design philosophy, but means a stale trip-tracking UI for a few seconds after backgrounding/foregrounding until the library's own reconnect kicks in. Worth verifying under real device lock/unlock cycles. The same service's `ReceiveMessage` listener (added for Chat) inherits this — and also requires consumers to dedupe by `messageId` since the backend pushes that event to both `user-{receiverId}` and `trip-{tripId}` groups (handled in `ConversationDetailController._upsertMessage`).

---

## Security concerns

(Several of these overlap with "Potential risks" above; grouped here for a security-focused read.)

- CORS: wildcard origin + `AllowCredentials()` (#7).
- Swagger UI reachable in production (#8).
- SignalR detailed errors unconditional (#9).
- Secrets as committed placeholders, dependent on environment override discipline (#10).
- Non-cryptographic RNG for OTP codes (#11).
- No DB-level uniqueness on the phone number that gates login (#12).
- `ChangeUserRole` has no allow-list (#3) — a privilege-escalation-adjacent input-validation gap if this endpoint is ever reachable by a less-trusted Admin tier or compromised Admin session.
- No referential integrity on the complaints/violations subsystem (#14) — not directly exploitable, but undermines the audit trail that subsystem exists to provide.
- Identity password rules are configured to effectively no-op (`RequireDigit/RequireLowercase/RequireUppercase/RequireNonAlphanumeric = false`, `RequiredLength = 1`, `Program.cs:46-50`) — **not a bug**, since this app is passwordless/OTP-only and Identity's password machinery is just along for the ride, but worth being aware of if password-based login is ever added later without revisiting this config.

---

## Verification note

Items #1, #2, #17 above (the `UserBlocksController` claim-name bug, the `BaseController` null-deref, and the `NotificationsController` unused-`DbContext` injection) were independently re-read and confirmed directly against the current working tree as part of producing this document, in addition to the original research pass. Item #6 (the Riverpod `build()`/`refresh()` race) was discovered and confirmed via a real failing/then-passing test while implementing the Chat feature on 2026-06-20, not by static reading alone. Items #16 (no driver-approval document fields), the `DriverAssignmentManualController`/`ComplaintsController` bullet in #19, and #32 (unbounded complaints/violations loads) were discovered while building the Admin MVP on 2026-06-20, by reading the actual repository/controller code each screen calls — not assumed from the DTO shapes alone. The remaining items were produced by a single thorough code-reading pass per repository and have not all been independently re-verified line-by-line a second time — treat file:line citations as accurate as of 2026-06-20, but re-check before relying on them if significant time has passed or these areas have since been edited.
