# PRE_DEMO_CHECKLIST — TaxiApp Graduation Rehearsal

Generated 2026-06-20. Companion to `DEMO_FLOW.md` (the rehearsal script) and `TEST_ACCOUNTS.md` (exact accounts/data). Audit and planning only — nothing in this document has been implemented or fixed.

This document found **three configuration-level issues that will silently break the entire demo** if not addressed beforehand. None of them are bugs in application logic — all three are missing/placeholder *configuration* (API keys, SMS credentials, one data row), discovered by tracing the exact code paths the demo will exercise. They were not caught by any previous session because `flutter analyze`/`flutter test`/`dotnet build` cannot detect "this third-party credential is fake."

---

## 🔴 Section A — Critical: will break the demo if not fixed, in priority order

### A.1 — A driver can never enter the queue for the very first time

**This is the most severe finding in this audit.** Traced directly in code:

- `DriverTripsController.EnterQueue` → `DriverAssignmentRepository.EnterQueueAsync` requires `driver.Status == DriverStatus.available` ([DriverAssignmentRepository.cs:1417](TaxiApp.Backend.Infrastructure/Repositories/DriverAssignmentRepository.cs#L1417)).
- A freshly registered driver is set to `DriverStatus.offline` explicitly ([AuthRepository.cs:249](TaxiApp.Backend.Infrastructure/Repositories/AuthRepository.cs#L249)).
- When Admin approves the driver, it is set to `DriverStatus.offline` **again**, explicitly ([DriverApprovalRepository.cs:73](TaxiApp.Backend.Infrastructure/Repositories/DriverApprovalRepository.cs#L73)).
- Assigning a vehicle (`AssignVehicleToDriver`) never touches `Status` at all.
- The **only** two places in the entire backend that ever set `Status = DriverStatus.available` are (1) when a trip's last order finishes, and (2) when a driver cancels an active trip for a non-accident reason — both of which require the driver to already be on a trip. There is no "go online" / "set available" endpoint anywhere in `DriversController` or `DriverTripsController`, and no admin action sets it either.
- The Flutter "Enter Queue" button is gated client-side on the exact same condition (`DriverProfile.canEnterQueue` → `status == DriverStatus.available`, [driver_profile.dart:49](../TaxiApp.Mobile/lib/features/driver/domain/entities/driver_profile.dart#L49)), so the button will simply never enable for a driver who has never completed a trip.

**Net effect: every demo driver account, no matter how well registered/approved/vehicle-assigned, will be permanently stuck at "offline" and the Enter Queue button will never light up.** This blocks the entire Driver and Admin-monitoring portions of the demo, since no order can ever be auto- or manually-matched to a driver who never enters the queue.

**Recovery (data fix, not a code change)** — run once per demo driver account, after approval + vehicle assignment, before rehearsing or demoing:
```sql
UPDATE Drivers SET Status = 0 WHERE UserId = '<driver's ApplicationUser.Id>';
```
(`0` = `available`, per the `DriverStatus` enum in `Driver.cs`.) Do this for every driver account you intend to use, every time you reset/reseed the database. This is a workaround, not a fix — the real fix is a one-line "go online" endpoint, which is out of scope for this audit per the no-code instruction. Flag this to whoever does the next implementation pass.

### A.2 — Twilio SMS credentials are placeholders — nobody can log in or register

`appsettings.json` has `Twilio: { AccountSid: "YOUR_SID", AuthToken: "YOUR_TOKEN", FromPhone: "YOUR_PHONE" }`. `SmsService.SendSms` wraps the real Twilio API call in a try/catch that returns `false` on any failure ([SmsService.cs:22](TaxiApp.Backend.Infrastructure/Repositories/SmsService.cs#L22)) — with placeholder credentials, this will fail every time.

- **Login** (`AuthRepository.LoginAsync`) explicitly checks the result and returns `"فشل إرسال الرسالة، حاول لاحقاً"` (SMS failed) if `SendSms` returns `false` — login will not proceed for **any** role, including the seeded Admin account, since Admin login also goes through phone+OTP with no bypass.
- **Registration** (`RegisterPassengerAsync`/registering a driver) does **not** check the return value at all — it will claim `"تم إرسال رمز التحقق إلى هاتفك"` (sent successfully) even when the SMS silently failed. Don't trust this response; only trust an SMS actually arriving on the phone.
- There is no dev-mode bypass that returns the OTP in the API response, despite a stale Arabic comment in `AccountController.Login` claiming otherwise — the comment doesn't match the current code.

**Recovery**: configure real Twilio credentials (`AccountSid`, `AuthToken`, `FromPhone`) before demo day — ideally in `appsettings.Development.json` or via `dotnet user-secrets`, not committed into `appsettings.json`. If using a Twilio **trial** account, every recipient phone number (every test account's phone) must be pre-verified in the Twilio console, or sends to that number will fail even with valid credentials. **Do a real test login with every demo phone number at least a day before the demo** — don't discover this live.

### A.3 — Google Maps API key is a placeholder — auto-dispatch will never match a driver

`appsettings.json` has `GoogleMaps: { ApiKey: "YOUR_API_KEY_HERE" }`. Traced the consequence fully:

- `GoogleMapService.GetDistancesAsync`/`GetETAAsync` call the real Google Distance Matrix/Directions APIs and return `TimeSpan.MaxValue` (or `""` for the polyline) on any failure, including an invalid API key ([GoogleMapService.cs](TaxiApp.Backend.Infrastructure/Helper/GoogleMapService.cs)).
- `DriverAssignmentRepository`'s matching logic filters out every candidate whose ETA is `TimeSpan.MaxValue`, then returns `"No valid ETA results"` if the candidate list ends up empty ([DriverAssignmentRepository.cs:426-433](TaxiApp.Backend.Infrastructure/Repositories/DriverAssignmentRepository.cs#L426), and two more identical patterns at lines 612 and 870).
- With a placeholder key, **every** ETA call fails, so **every** driver gets filtered out, every time — meaning **auto-dispatch can never match a single order to a driver**, regardless of how many drivers are online or how close they are.
- The route line shown during a trip (`TripRoutingService.RecalculateTripAsync` → `GetRoutePolylineAsync`) has the same dependency — it will return an empty polyline with a bad key, so the drawn route line will not appear even once a trip exists.

**Two recovery paths**:
1. **Best**: configure a real Google Cloud API key with the **Directions API** and **Distance Matrix API** enabled, billing active (covers demo-scale usage on the free tier). Test one real order end-to-end before demo day to confirm a driver actually gets matched.
2. **Fallback that needs no API key at all**: switch the Admin app's dispatch mode to **Manual** (`DriverAssignmentManualController.SetMode`, already built and wired into the Admin "Manual Assignment" screen). This sets `SystemSettings.Mode = Manual`, which is checked directly inside `DriverAssignAsync` before any Google Maps call is made ([DriverAssignmentRepository.cs:72-74](TaxiApp.Backend.Infrastructure/Repositories/DriverAssignmentRepository.cs#L72) and the background retry loop in `TripOfferBackgroundService.cs:65-67`) — confirmed this code path never touches `IMapService`. In Manual mode, the Admin picks the driver directly from the "assignable drivers" list with no ETA computation at all. **Caveat**: this only fixes *matching*. The drawn route line / ETA number shown afterward during the trip still goes through `TripRoutingService`, which still calls Google Maps — so even in Manual mode, expect the route polyline and ETA minutes to be missing/zero if the key is still a placeholder. The live driver-location dot itself is unaffected either way, since real GPS positions never go through Google Maps.

**If you cannot get a real Google Maps key before the demo, recovery path 2 (Manual mode) is the only way the core "request → dispatch → trip" loop works at all.** Decide which path you're taking and rehearse with that exact configuration — don't assume Auto mode "mostly works."

---

## 🟠 Section B — Environment & network setup

- [ ] **Backend is running the `https` launch profile** (`https://localhost:7022` / `http://localhost:5033`), `ASPNETCORE_ENVIRONMENT=Development`. Swagger auto-opens at `/swagger` — useful for a quick manual endpoint check if something looks wrong mid-rehearsal.
- [ ] **SQL Server is reachable** at `Server=.` (local default instance, Windows auth, per `appsettings.json`). Confirm the instance is running before starting the API.
- [ ] **Database schema is current.** This backend does **not** auto-apply migrations on startup — `Program.cs` only seeds roles + the Admin user, it never calls `Database.Migrate()`. If any migration has been added since the DB was last updated, run `dotnet ef database update` (or Visual Studio's Update-Database) **before** starting the API, or the first request touching a missing column/table will throw a raw SQL exception mid-demo.
- [ ] **Flutter base URL matches your device type.** `ApiConstants.baseUrl` defaults to `http://10.0.2.2:5033/api` (Android-emulator loopback alias). For:
  - iOS simulator / desktop: `flutter run --dart-define=API_BASE_URL=http://localhost:5033/api`
  - **A real physical phone** (the case most worth rehearsing, see Section E): `flutter run --dart-define=API_BASE_URL=http://<your-machine's-LAN-IP>:5033/api` — find the IP with `ipconfig` (Windows). The phone and the backend machine must be on the **same Wi-Fi network**; corporate/guest Wi-Fi that isolates clients from each other will break this silently (requests will just time out). Prefer a personal hotspot or a known-open home network for the demo if in doubt.
  - Confirm Windows Firewall allows inbound connections to port 5033 (and 7022 if testing HTTPS) from the local network — a fresh firewall prompt the first time you run with a LAN IP is normal; allow it.
- [ ] **CORS is already wide open** (`AllowAll` policy, `SetIsOriginAllowed(_ => true)`) — no action needed here, just confirming it won't be the thing that breaks on the day.
- [ ] **SignalR hub is reachable** at `{serverRoot}/notificationHub` (same host/port as the API, not under `/api`). If live tracking/chat/notifications don't update, check this is resolving correctly for your chosen base URL override, not just the REST calls.

## 🟢 Section C — Permissions & device prep

- [ ] **Location permission is requested lazily** — only the first time a trip actually starts for that driver session, never at login, never for passengers (`DriverLocationService.ensurePermission`, [driver_location_service.dart](../TaxiApp.Mobile/lib/core/location/driver_location_service.dart)). Don't expect the OS permission prompt before that point — if you're rehearsing the "driver accepts an order" step, be ready to tap "Allow" on the location prompt **right then**.
- [ ] Android manifest already declares `INTERNET`, `ACCESS_FINE_LOCATION`, `ACCESS_COARSE_LOCATION` — confirmed present, no action needed (this was a real bug found and fixed earlier in this project; don't reintroduce it by hand-editing the manifest).
- [ ] If rehearsing on a real device, make sure GPS/Location Services is toggled on at the OS level before the trip-start step — `ensurePermission()` checks `Geolocator.isLocationServiceEnabled()` first and silently returns `false` (no tracking, no crash) if location services are off system-wide, which would look like "nothing happened" rather than a clear error.
- [ ] Two separate physical devices (or one device + one emulator) are needed to rehearse Passenger-sees-Driver-move in real time — a single device can't be both halves of the live-tracking loop at once.

## ⚪ Section D — Known non-blocking risks (carried over from `GRADUATION_BLOCKERS.md`, still open)

These won't stop the demo from running but are worth a verbal answer if a grader pokes at them:
- `UserBlocksController.ToggleUserBlock` 401s for every caller (claim-lookup bug) — only reachable via Swagger/Postman directly, not from any app screen.
- `BaseController.CheckUserAccessAsync` doesn't null-check before dereferencing a deleted/stale account — low risk in a scripted demo.
- No automated test suite on the backend (`0` test projects); Flutter has 3 test files covering only the route-parsing logic, not integration.
- No "leave queue" feature exists, backend or Flutter — if asked, the honest answer is "queue entry exists, queue exit is a known gap."

## ✅ Section E — Final go/no-go list, morning of the demo

1. [ ] Confirm A.1, A.2, A.3 above are each either fixed (real Twilio/Google creds) or have their fallback in place (DB status patch / Manual dispatch mode) — **do this the night before, not the morning of.**
2. [ ] Run the entire Passenger → Driver → Admin script in `DEMO_FLOW.md` once, start to finish, on the actual device(s)/network you'll use for the real demo — not just emulators on the same machine as the backend. This is the single highest-value thing this audit could not do for you (see Section "Real-device verification list" below).
3. [ ] Confirm both apps point at the same backend instance the rehearsal used — if the backend was restarted between rehearsal and demo, `SystemSettings.Mode` and any in-memory cache (the 5-minute mode cache, OTP cooldowns) reset; that's fine, just be aware timings reset too.
4. [ ] Have the SQL query from A.1 ready in a saved script/snippet, in case a driver account needs the `Status` patch reapplied after a database reset.
5. [ ] Charge both demo phones, mute notifications unrelated to the app, and pre-clear any old conversations/orders from previous rehearsals that might confuse "is this the live data or leftover test data?" during the actual demo.
