# GRADUATION_BLOCKERS — TaxiApp

Generated 2026-06-20 as part of the complete project audit (see `MISSING_FEATURES.md` for the full catalog, `IMPLEMENTATION_STATUS.md` for area-by-area status). This document answers one question only: **what, if left unfixed, will actually break or embarrass the graduation demo?**

Audit only — nothing below has been implemented.

---

## How to read this list

"Blocker" here means one of two things:
- **Functional blocker**: a core role workflow cannot be completed in the app at all, with no workaround short of manually editing the database.
- **Demo-risk blocker**: something that will very likely come up if a grader pokes at the app even slightly off the happy path (the most obvious next click after the main flow), and has no graceful fallback.

Items that are real gaps but *don't* meet either bar (e.g. no settings screen, no favorite locations) are tracked in `MISSING_FEATURES.md` instead — they make the app feel less complete, but won't make a demo fail.

---

## 🔴 Functional blockers — all 3 fixed as of 2026-06-20 (kept here for the record)

### 1. ~~A freshly registered driver cannot receive any orders — there is no way to give them a vehicle~~ — Fixed 2026-06-20
**The original chain**: `DriverProfile.canEnterQueue` requires `hasVehicle == true` → a `Vehicle` row was only ever created by `VehicleRepository.AddVehicel` → that method was only called from `VehiclesController.AddVehicle` → that endpoint had zero Flutter callers, anywhere, on any screen, for any role.

**Fix shipped**: the "real fix" option below was implemented — a minimal vehicle-registration form on the Admin-side Driver Approval detail sheet, calling the two existing (already built, already Admin-authorized) `AddVehicle`/`AssignVehicleToDriver` endpoints back to back. No backend change, no fleet-management UI (no vehicle list/edit/unassign screens) — just enough to unblock the chain above. See `MISSING_FEATURES.md` §8.1 for the full writeup and the verification trace confirming this satisfies every downstream consumer (`hasVehicle`/`canEnterQueue` on the Driver Dashboard, and the dispatch-matching queries that decide who gets offered an order).

**No workaround needed anymore** — the previous recommendation (manually seed a `Vehicle` row per demo account before the demo) is obsolete; an Admin can now do this from within the app for any driver, at any time.

### 2. ~~A passenger can never submit a rating — only ever see one that doesn't exist~~ — Fixed 2026-06-20
`POST /PassengerTrips/rate-driver` was fully built and worked; `OrderDetailScreen` only ever *displayed* a rating, never collected one.

**Fix shipped**: a tap-to-open Material 3 star-picker + optional-comment dialog appears in place of the read-only rating card once a trip is completed and unrated, calling the existing endpoint via a new `OrderDetailController.rate` method — no backend change. Verified the new `Rating` row correctly feeds not just the passenger's own screen but also the driver's aggregate rating shown to Admins (`DriverApprovalRepository`) and used in dispatch driver-matching (`GetDriverWeightedRating`) — see `MISSING_FEATURES.md` §10.1 for the full trace. One known, intentionally-unmitigated edge: the backend only accepts a rating within 30 minutes of trip completion; a late attempt surfaces that as a normal error message rather than being hidden client-side.

### 3. ~~A driver has no way to cancel a trip once accepted~~ — Fixed 2026-06-20
`POST /DriverTrips/cancel-trip/{tripId}` was fully built (with a reason enum: driver issue / vehicle problem / accident / emergency); nothing in the Driver app called it.

**Fix shipped**: a cancel button + reason-picker dialog on `ActiveTripCard`, calling the existing endpoint via a new `DriverActiveController.cancelTrip` method — no backend change. Verified the backend's actual behavior on cancellation (it resets the trip to `SearchingDriver` and immediately tries to re-dispatch to another driver, reusing the same `TripId`) composes correctly with the existing passenger-side code with zero additional changes there — see `MISSING_FEATURES.md` §9.1 for the full trace.

---

## 🟠 High demo-risk — fix if time allows, have a verbal answer ready if not

### 4. `UserBlocksController.ToggleUserBlock` 401s for every caller, always
Not reachable from any Flutter screen, so it won't surface in a normal click-through demo. **But if a grader opens Swagger/Postman directly and tries it** (plausible for a backend-focused evaluator), it will visibly fail. One-line fix (`User.FindFirstValue(ClaimTypes.NameIdentifier)` instead of `"UserId"`) — **5 minutes**, essentially free insurance if you have the time.

### 5. A deleted/stale account crashes with a 500 instead of a clean error
`BaseController.CheckUserAccessAsync` doesn't null-check before dereferencing. Unlikely to trigger in a scripted demo, but a real risk if test accounts get created/deleted/recreated rapidly while setting up demo data right before presenting. **~15 minutes** to add the null check.

### 6. No automated tests on either side
Not something a grader will "click into," but very likely to come up if the rubric explicitly asks about testing practices, or if something breaks during setup and there's no test suite to quickly confirm what's still working. If you only have time for one thing here: a handful of widget tests around the Auth/login flow and Driver accept/reject flow are the highest-value few hours, since those are the two flows most likely to be re-run repeatedly while rehearsing the demo.

### 7. Driver "leave queue" doesn't exist (backend or Flutter)
Low risk for a *scripted* demo (you control when drivers enter the queue), but if a grader asks "can a driver go off-duty?", the honest answer today is no, and there's no quick UI-only fix since the backend endpoint itself doesn't exist either. If asked, the accurate answer is: "queue entry exists, queue exit is a known gap, tracked in `TODO.md`."

---

## Items considered and explicitly NOT classified as blockers

These are real, documented gaps (see `MISSING_FEATURES.md`) but were deliberately excluded from this list because they fail both the "functional blocker" and "demo-risk" tests above:

- No Admin profile/settings/vehicle-fleet/user-management/leaderboard screens — these are visible scope limitations, not broken functionality; the Admin MVP explicitly shipped 5 of these areas this session and the other 4 were knowingly descoped ("graduation demo only... skip advanced reports").
- No favorite-locations, order-editing, or complaint-filing UI — real gaps, but none of them sit on the critical path of "register → order → dispatch → trip → complete," which is almost certainly the actual demo script.
- CORS/Swagger/secrets hardening — production-readiness concerns that don't affect a local/demo-environment run at all.
- ~~`RouteUpdated` not reaching the passenger (no drawn route line)~~ — fixed 2026-06-20, see `MISSING_FEATURES.md` §4.1/§11.1. Also surfaced and fixed a separate route-planning bug along the way (the dropoff leg was never computed for single-order trips).
- ~~The live driver-marker dot still updates correctly~~ — **this claim was wrong** when first written and has since been fixed. A deeper trace found the Driver app never acquired or sent real GPS at all (no location package, no native permissions, nothing ever called the backend's `SendLocation` hub method) — `Driver.LastLat/LastLng` was permanently `null`, so no driver marker would ever have appeared during a demo trip. This was actually the most severe finding in the whole audit (worse than the original 3 Criticals, since it's passive — it would surface without anyone needing to click an edge-case button). Fixed this session: see `MISSING_FEATURES.md` §11.2. Verified by code trace and `flutter analyze`/`flutter test`; not yet verified on a real device against a live backend.

---

## Recommended pre-demo checklist, in priority order

All 3 original Critical items are now fixed. What's left is verification, not implementation:

1. 🔍 **Run a real end-to-end rehearsal on an actual device or emulator** — every fix shipped today (GPS tracking, route drawing, vehicle assignment, driver cancel-trip, rating submission) was verified by full code-path tracing, not a live device/backend round-trip, which isn't possible from this tool environment. Specifically confirm: location-permission prompt appears and a passenger sees the driver marker move along a drawn route; an Admin can assign a vehicle and that driver can immediately enter the queue; a driver can cancel a trip and the passenger's screen reverts to "searching"; a passenger can rate a completed trip and see it reflected back immediately.
2. 🔧 Fix the `UserBlocksController` claim bug and the `BaseController` null-check (High #4-5) — ~20 minutes combined, do these regardless of time pressure since they're nearly free.
3. Optional, time-permitting: the High-risk items below (#4-7) and anything in `MISSING_FEATURES.md`'s remaining Medium/Low items.

Everything else in `MISSING_FEATURES.md` is safe to leave for after graduation.
