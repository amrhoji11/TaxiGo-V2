# UNUSED_ENDPOINTS — TaxiApp

Generated 2026-06-20 by cross-referencing a complete backend controller/action inventory against a complete grep of every endpoint-constant reference in `TaxiApp.Mobile/lib/core/constants/api_constants.dart` and every `*_api.dart` datasource file. Audit only.

**Update (same day, later sessions)**: `DriverTrips/cancel-trip`, the `Vehicles/AddVehicle`+`AssignVehicleToDriver` pair, and `PassengerTrips/rate-driver` were all wired up after this doc was first generated (see `MISSING_FEATURES.md` §9.1/§8.1/§10.1). The numbers and tables below have been updated in place to reflect that; they no longer match the original `~45 unused (49%)` headline.

**Method**: every backend action was checked against the Flutter `*Endpoints` constant classes (`AuthEndpoints`, `OrdersEndpoints`, `NotificationsEndpoints`, `PassengersEndpoints`, `DriversEndpoints`, `DriverTripsEndpoints`, `MessagesEndpoints`, `AdminEndpoints`, `DriverApprovalsEndpoints`, `DriverAssignmentManualEndpoints`, `ComplaintsEndpoints`). Every constant member that exists was independently confirmed to have at least one real call site — **there are zero "defined but unused" Flutter-side constants**. So every gap below is a genuine backend action with no Flutter constant/call site for it at all, not a wiring slip on the Flutter side.

---

## Headline numbers

| | Count |
|---|---|
| Backend controllers | 19 (18 with actions; `TestsController` is an empty scaffold) |
| Backend actions, total | ~91 |
| Backend actions called by Flutter | 50 (was 46) |
| Backend actions with **no Flutter caller** | **~41 (≈45%)**, was ~45 (≈49%) |

Roughly half the backend's API surface has no mobile client today. Most of this is explained by entire admin-management surfaces that were never in scope for the graduation-MVP Admin app (vehicle fleet, broad user management, settings, favorites) rather than by broken wiring — see the per-controller verdicts below.

---

## Per-controller breakdown

### Fully used (0 unused actions)
| Controller | Actions | Notes |
|---|---|---|
| `AccountController` | 10/10 | Full auth lifecycle — register, confirm, login, OTP verify, phone change, refresh, logout. |
| `NotificationsController` | 3/3 | List, mark-as-read, mark-all-read. |
| `MessagesController` | 3/3 | Send, conversation, conversations. |
| `DriverApprovalsController` | 4/4 | Pending list, approve, reject, detail. |
| `DriverAssignmentManualController` | 5/5 | Manual assign (order/trip), mode get/set, assignable-drivers. |

### Mostly used, small gaps
| Controller | Used / Total | Unused actions | Verdict |
|---|---|---|---|
| `OrdersController` | 4/5 | `PUT /Orders/{id}` (EditOrder) | **Concerning** — a real, demoable feature with no UI (`MISSING_FEATURES.md` §2.1). |
| `PassengersController` | 2/3 | `GET /Passengers/trips-report` | Acceptable to leave — a secondary stats view, not core. |
| `DriversController` | 2/3 | `GET /Drivers/my-trips-report` | Acceptable to leave — same rationale. |
| `DriverTripsController` | 8/11 (was 7/11) | `accept-trip`, `reject-trip`, `start-trip` | **Mixed** — `cancel-trip` is now used (fixed, see `MISSING_FEATURES.md` §9.1); `start-trip` is fine to leave (the Pickup action already covers starting a trip by design); `accept-trip`/`reject-trip` (whole-trip/shared-ride offers) are **concerning** if shared trips can ever actually be dispatched (`MISSING_FEATURES.md` §9.3). |
| `ComplaintsController` | 4/6 | `POST /api/orders/{orderId}/complaints` (passenger files a complaint), `GET driver/{id}/violations-count` | **Mixed** — the filing endpoint is **concerning** (no passenger-side UI exists to use the Admin review screen built this session, `MISSING_FEATURES.md` §8.6); the violations-count endpoint is fine to leave (a minor helper, not surfaced separately in the Admin UI which already shows full violation rows). |
| `AdminController` | 2/12 | `edit`, `profile`, `SoftDeleteDriver`, `GetAllDrivers`, `RestoreDriver`, `SoftDeletePassenger`, `GetAllPassengers`, `RestorePassenger`, `profile/{id}`, `top-drivers` | **Mixed** — `edit`/`profile` (no Admin profile screen) is a visible completeness gap; the soft-delete/restore/list-all-users actions reflect "no Admin user-management screen" (a real, acknowledged scope cut); `top-drivers` was explicitly descoped this session ("skip advanced reports"). |
| `VehiclesController` | 2/8 (was 0/8) | `AddVehicle`, `AssignVehicleToDriver` now used (fixed, see `MISSING_FEATURES.md` §8.1) — `GetAll`, `{id}`, `Edit`, `GetUnassignedAsync`, `Unassign`, `status` | **Mixed** — the two actions needed to unblock driver dispatch are now wired up; the rest (list/edit/unassign/toggle-status) are deliberately not built — full fleet management was explicitly out of scope. |

### Entirely unused (0 used actions)
| Controller | Actions | Verdict |
|---|---|---|
| `UsersController` | 0/5 | Acceptable scope cut — broad user administration (list/search/deactivate/change-role) was never part of the declared Admin MVP. |
| `UserBlocksController` | 0/2 | Acceptable scope cut for the same reason — and the block-toggle action is independently broken regardless of usage (`MISSING_FEATURES.md` §1.1). |
| `SettingsController` | 0/7 | Acceptable scope cut — no settings screen exists for any role yet (`MISSING_FEATURES.md` §8.4). |
| `FavoriteLocationsController` | 0/3 | Acceptable scope cut — no favorites UI exists yet (`MISSING_FEATURES.md` §8.7). |
| `TestsController` | 0/0 | N/A — empty scaffold, nothing to use. Safe to delete (`MISSING_FEATURES.md` §1.6). |

`PassengerTripsController` (1/1, was 0/1) moved out of this section — `rate-driver` is now used, fixed, see `MISSING_FEATURES.md` §10.1.

---

## Full action-level list — every unused backend action

```
OrdersController
  PUT  /Orders/{id}                                  — EditOrder            [Medium — real gap]

PassengersController
  GET  /Passengers/trips-report                       [Low — nice to have]

DriversController
  GET  /Drivers/my-trips-report                        [Low — nice to have]

DriverTripsController
  POST /DriverTrips/accept-trip/{tripId}               [High — shared-trip path has no UI]
  POST /DriverTrips/reject-trip/{tripId}               [High — shared-trip path has no UI]
  POST /DriverTrips/start-trip/{tripId}                [Acceptable — Pickup covers this by design]

ComplaintsController
  POST /api/orders/{orderId}/complaints                [Medium — no passenger filing UI]
  GET  /Complaints/driver/{driverId}/violations-count   [Low — minor helper, not needed separately]

AdminController
  PUT  /Admin/edit                                     [Medium — no Admin profile screen]
  GET  /Admin/profile                                  [Medium — no Admin profile screen]
  DELETE /Admin/SoftDeleteDriver/{id}                   [High — no Admin user-mgmt screen]
  GET  /Admin/GetAllDrivers                            [High — superseded by assignable-drivers for the one screen that needs a driver list; otherwise unused]
  PUT  /Admin/RestoreDriver/{id}                        [High — no Admin user-mgmt screen]
  DELETE /Admin/SoftDeletePassenger/{id}                [High — no Admin user-mgmt screen]
  GET  /Admin/GetAllPassengers                          [High — no Admin user-mgmt screen]
  PUT  /Admin/RestorePassenger/{id}                     [High — no Admin user-mgmt screen]
  GET  /Admin/profile/{id}                              [High — no Admin user-mgmt screen]
  GET  /Admin/top-drivers                               [Nice to have — explicitly descoped this session]

VehiclesController
  GET    /Vehicles/GetAll                               [Medium — no vehicle-list UI; not a blocker now that AddVehicle/AssignVehicleToDriver are used]
  GET    /Vehicles/{id}                                  [Medium — same]
  PUT    /Vehicles/{id}/Edit                             [Medium — same]
  GET    /Vehicles/GetUnassignedAsync                    [Medium — same]
  POST   /Vehicles/{vehicleId}/Unassign                  [Medium — same]
  PATCH  /Vehicles/{vehicleId}/status                    [Medium — same]

UsersController
  GET   /Users/GetAll                                   [Acceptable scope cut]
  GET   /Users/GetUserById/{userId}                      [Acceptable scope cut]
  GET   /Users/SearchUsers                               [Acceptable scope cut]
  PATCH /Users/ToggleUserActive/{userId}                 [Acceptable scope cut]
  PATCH /Users/ChangeUserRole/{userId}                   [Acceptable scope cut — and has no role allow-list regardless, see MISSING_FEATURES.md §1.3]

UserBlocksController
  PATCH /UserBlocks/{userId}/ToggleUserBlock             [Acceptable scope cut — and independently broken, see MISSING_FEATURES.md §1.1]
  GET   /UserBlocks/GetAllBlocks                         [Acceptable scope cut]

SettingsController
  PUT  /Settings/Language                                [Medium — no settings screen, any role]
  GET  /Settings/language                                [Medium — same]
  PUT  /Settings/darkmode                                [Medium — same]
  GET  /Settings/darkmode                                [Medium — same]
  PUT  /Settings/Notifications                           [Medium — same]
  GET  /Settings/ViewNotificationsStatus                 [Medium — same]
  GET  /Settings/ContactWithTaxiGo                        [Low — minor convenience link]

FavoriteLocationsController
  POST   /FavoriteLocations/AddFavoriteLocation           [Medium — no favorites UI]
  GET    /FavoriteLocations/GetAllFavoriteLocations        [Medium — same]
  DELETE /FavoriteLocations/DeleteFavoriteLocation/{id}     [Medium — same]
```

---

## No reverse problem found

The Flutter agent's grep confirmed the opposite direction is clean: **every** `*Endpoints.*` constant defined in `api_constants.dart` is referenced from a real datasource call site, and **every** `_dio.get/post/put/patch/delete` call in every `*_api.dart` file uses a constant rather than a hardcoded path string. There is no "Flutter calls a route that doesn't exist on the backend" problem anywhere in the app.

---

## Recommendation summary

- **Fix before demo**: nothing left in this category — `DriverTrips/cancel-trip`, the core `VehiclesController` actions (`AddVehicle`/`AssignVehicleToDriver`), and `PassengerTrips/rate-driver` are all fixed.
- **Fix if time allows**: `Orders/{id}` edit, the `Complaints` passenger-filing endpoint, `DriverTrips/accept-trip`/`reject-trip` (or explicitly confirm shared trips are out of scope and won't be triggered), the remaining `VehiclesController` actions (list/edit/unassign/toggle-status) if full fleet management ever becomes in-scope.
- **Safe to leave entirely unused past graduation**: `UsersController`, `UserBlocksController`, `SettingsController`, `FavoriteLocationsController`, `Admin/top-drivers`, `Drivers/my-trips-report`, `Passengers/trips-report`, `Complaints/violations-count`.
