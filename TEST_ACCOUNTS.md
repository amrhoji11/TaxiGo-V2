# TEST_ACCOUNTS — TaxiApp Graduation Rehearsal

Generated 2026-06-20. Companion to `PRE_DEMO_CHECKLIST.md` (setup/config blockers) and `DEMO_FLOW.md` (the rehearsal script). Audit and planning only — no accounts have actually been created by this pass; this is the list of what to create and how.

**Read `PRE_DEMO_CHECKLIST.md` §A.2 first.** Every account below logs in via real SMS OTP (phone + 6-digit code, no password ever checked at login). That means every phone number listed here must be a real, reachable number, and Twilio must be configured with working credentials before any of this works. **Register and verify all accounts at least one full day before the demo** — first-time SMS delivery is exactly the kind of thing that surfaces problems (wrong country code formatting, a Twilio trial account's unverified-recipient restriction, a typo'd phone number) that you do not want to discover live.

---

## How login/registration actually works (so you're not surprised)

- **Login** = phone number + country code only. The backend looks up the user by phone, sends a 6-digit OTP via SMS (expires in **2 minutes**, one resend allowed per **60 seconds**, 5 wrong attempts locks you out for ~2 minutes), then `POST /Account/verify-otp` exchanges the code for a JWT. There is no password field anywhere in `LoginRequest`.
- **Registration** (Passenger/Driver) = submit basic profile info, receive a different 6-digit OTP (random, **5-minute** expiry, no resend cooldown enforced), confirm it via `POST /Account/confirm-register-passenger` or `confirm-register-driver` to actually create the account.
- A **Driver** registration additionally requires Admin approval (`DriverApprovals`) before that account can log in at all — attempting to log in as a pending/rejected driver returns a clear Arabic message instead of proceeding to OTP.
- Phone format the backend expects: `CountryCode` + `PhoneNumber` matching `^\d{9,10}$` (digits only, no leading `+` or `0` trunk prefix in the `PhoneNumber` field itself — the two are concatenated server-side via `PhoneHelper.BuildInternationalPhone`).

---

## Admin account — already seeded, no registration needed

A single Admin account is created automatically the first time the backend starts (`DbSeeder.SeedAdminAsync`, runs unconditionally on every startup but only inserts if not already present):

| Field | Value |
|---|---|
| Phone | `0595541748` (already in local format — confirm what country code your `PhoneHelper.BuildInternationalPhone` expects this to combine with, e.g. Jordan `+962`) |
| Username | `amrhoji` |
| Email | `admin@taxiapp.com` (pre-confirmed, irrelevant to login) |
| Password | `Admin@123` (set at creation because `UserManager.CreateAsync` requires one, but **login never checks it** — OTP-only, same as every other role) |
| Role | `Admin` |

**This account still needs a working Twilio send to log in** (see `PRE_DEMO_CHECKLIST.md` §A.2) — being seeded only means the account row exists, not that login is exempt from the OTP flow.

If this phone number isn't one you can actually receive SMS on, you have two options: get Twilio properly configured and send to it anyway (if it's a real, working number you just don't have on hand), or register a second Admin manually — but there is no self-service "become Admin" registration endpoint, so a second Admin would need a direct database role assignment, which is out of scope for this audit's "no code/no implementation" instruction. Recommend treating the seeded account's phone as fixed and make sure *that specific number* is reachable and Twilio-verified.

---

## Passenger accounts — register fresh, recommend 2

Register via the app's normal Passenger sign-up flow (`RegisterFormScreen(role: AppRole.passenger)`). No approval step — usable immediately after OTP confirmation.

| Slot | Suggested label | Required fields | Notes |
|---|---|---|---|
| Passenger 1 | "Main demo passenger" | First name, last name, country code + phone, address (optional) | This is the one you'll run the entire Passenger script against. |
| Passenger 2 | "Spare / backup" | Same | Register this even if you don't plan to use it — if Passenger 1's phone has any SMS hiccup on demo day, you want a known-working fallback already verified, not a fresh registration attempted live. |

Required test data to have ready *before* registering: a real first/last name (cosmetic, shown in driver-facing trip details and chat), a real reachable phone number, and — for the Create Order step later — two real, distinct map locations within your test city for pickup/dropoff (pick two places you can recognize on the map at a glance, e.g. "my house" and "the office," so during the live demo you're not hunting for an unfamiliar address under time pressure).

---

## Driver accounts — register, approve, assign vehicle, then patch status

Driver setup has four sequential steps, each with its own data and its own opportunity to forget something. Recommend 2 driver accounts for the same "main + spare" reason as Passengers — also useful if you ever want to demo the "driver receives an offer while another driver is busy" nuance.

### Step 1 — Register
Via `RegisterFormScreen(role: AppRole.driver)`. Same phone/name fields as Passenger registration. Result: account exists with `ApprovalStatus = pending`, `DriverStatus = offline` — cannot log in yet.

### Step 2 — Admin approves
From the Admin app's Driver Approvals screen, approve the pending driver. Result: `ApprovalStatus = approved`, but `DriverStatus` is explicitly reset to `offline` again as part of approval — driver can now log in, but still can't go anywhere near the queue yet.

### Step 3 — Admin assigns a vehicle
Still from the Admin app (the Driver Approval detail sheet's vehicle-assignment form, added in an earlier fix this project). Required vehicle data per driver:

| Field | Type | Example |
|---|---|---|
| Plate number | string, max 20 chars, required | `D-12345` |
| Vehicle size | enum: `Small` / `Medium` / `Large` | `Medium` |
| Seats | int | `4` |
| Make | string | `Toyota` |
| Model | string | `Corolla` |
| Color | string | `White` |
| Year | int, optional | `2021` |
| Plate photo | optional file upload | skip for rehearsal unless testing the upload path specifically |

Result: the driver's profile now reports `hasVehicle = true`, and the Driver Dashboard's vehicle card shows these details. **The Enter Queue button still will not enable yet** — see Step 4.

### Step 4 — Patch the driver's status (required workaround, see `PRE_DEMO_CHECKLIST.md` §A.1)
Run once per driver account, after Step 3:
```sql
UPDATE Drivers SET Status = 0 WHERE UserId = '<that driver's ApplicationUser.Id>';
```
Without this, `canEnterQueue` will never be true and the Driver rehearsal script cannot proceed past "Login." This is the single most important step in this entire document — it is also the easiest one to forget, since nothing in the app's UI tells you it's missing; the Enter Queue button just silently never appears.

---

## Summary table — everything you need before rehearsal day

| Account | Phone (real, SMS-reachable) | Backend state required | Manual steps needed |
|---|---|---|---|
| Admin | `0595541748` (seeded) | exists automatically | confirm Twilio can actually reach this number |
| Passenger 1 | choose | registered + OTP-confirmed | none beyond registration |
| Passenger 2 (spare) | choose | registered + OTP-confirmed | none beyond registration |
| Driver 1 | choose | registered → approved → vehicle assigned → status patched | Admin approval, vehicle form, SQL status patch |
| Driver 2 (spare) | choose | same as Driver 1 | same as Driver 1 |

Do all of the above, then run the full script in `DEMO_FLOW.md` once before treating any of these accounts as "demo ready."
