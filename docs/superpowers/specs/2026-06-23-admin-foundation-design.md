# Admin Foundation Design

Date: 2026-06-23

## Goal

Add an admin foundation for Outfit Planner before public billing exists. The app will be released only for the owner for now, so Stripe checkout, public purchase flows, and user-facing paywall states are out of scope. The foundation must still model roles, premium entitlement, AI credits, subscriptions, and admin audit history in a way that can later connect to Stripe without rewriting the admin area.

The only current-user interface changes are:

- The account panel shows the authenticated user's visible role: `Free`, `Premium`, or `Admin`.
- The admin panel appears only for the single admin account.

Wardrobe, Builder, Calendar, try-on generation flows, and other existing user surfaces must keep their current behavior.

## Roles And Access

Visible roles are `Free`, `Premium`, and `Admin`.

The single admin account is fixed by normalized email:

```text
dmytro.bolibok@gmail.com
```

Backend role enforcement is authoritative. Frontend hiding is convenience only.

Rules:

- New accounts default to `Free`.
- The fixed owner email is always `Admin` when read through auth/session/admin flows.
- No other account can be assigned `Admin`.
- Admin can manually change ordinary users between `Free` and `Premium`.
- Admin routes return `401` for anonymous requests and `403` for authenticated non-admin users.
- `/api/auth/me` returns the visible role so the frontend can display it and gate the admin navigation.

## Paywall Foundation

This design does not enforce a paywall yet. It only stores and manages the data needed for a future paywall.

Foundation records:

- User role: current visible access tier.
- Entitlement: active premium state, source, start time, optional end time.
- Subscription record: manual subscription state now, Stripe-compatible source/provider fields later.
- Credit ledger: append-only credit grants, usage, refunds, and manual adjustments.
- Admin audit log: append-only record of admin actions.

Initial subscription source values:

- `Manual`: created or changed by the admin panel.
- `Stripe`: reserved for a future Stripe integration, not used by v1.

Initial credit ledger entry types:

- `ManualGrant`
- `ManualDebit`
- `TryOnDebit`
- `Refund`
- `Adjustment`

Try-on code may continue to estimate and confirm credits as it does today, but v1 does not block generation based on ledger balance.

## Backend Architecture

Keep onion dependencies:

- Domain defines role, entitlement, subscription, credit ledger, and audit entities/enums.
- Application owns admin use cases and repository interfaces.
- Infrastructure implements in-memory and PostgreSQL persistence.
- Api maps protected admin endpoints and auth/session contracts.

Admin logic should live in application services rather than inline route code where meaningful:

- `AdminService` for user search, user detail, role updates, credit adjustments, overview, and audit reads.
- Auth service continues to own login/session creation, but public auth user projection includes role.

The fixed admin email should be centralized in a small policy/helper so the same rule is applied in registration, login, session projection, and admin role mutation.

## Data Model

PostgreSQL migrations and the readable `database/schema.sql` snapshot should include:

- `users.role text not null default 'Free'`
- `user_entitlements`
  - `id uuid primary key`
  - `user_id text not null references users(id) on delete cascade`
  - `kind text not null`
  - `source text not null`
  - `starts_at timestamptz not null`
  - `ends_at timestamptz`
  - `created_at timestamptz not null`
  - `updated_at timestamptz not null`
- `subscription_records`
  - `id uuid primary key`
  - `user_id text not null references users(id) on delete cascade`
  - `source text not null`
  - `status text not null`
  - `provider_customer_id text`
  - `provider_subscription_id text`
  - `current_period_start timestamptz`
  - `current_period_end timestamptz`
  - `created_at timestamptz not null`
  - `updated_at timestamptz not null`
- `credit_ledger`
  - `id uuid primary key`
  - `user_id text not null references users(id) on delete cascade`
  - `entry_type text not null`
  - `amount integer not null`
  - `balance_after integer not null`
  - `reason text not null`
  - `related_try_on_job_id uuid`
  - `created_by_user_id text`
  - `created_at timestamptz not null`
- `admin_audit_log`
  - `id uuid primary key`
  - `admin_user_id text not null`
  - `action text not null`
  - `target_user_id text`
  - `summary text not null`
  - `metadata_json text`
  - `created_at timestamptz not null`

Indexes:

- users by normalized email and role.
- entitlements by user and active date range.
- subscriptions by user and provider IDs.
- credit ledger by user and created date.
- audit log by admin, target user, action, and created date.

In-memory storage must support the same behavior for tests and local no-Postgres runs.

## Admin API

All admin routes live under `/api/admin` and require the fixed admin role.

Endpoints:

- `GET /api/admin/overview`
  - Returns user counts by role, total credit balance, recent audit entries, try-on job counts by status, and system status summary.
- `GET /api/admin/users?q=&role=&offset=&limit=`
  - Returns paginated user rows with id, email, display name, role, premium state, credit balance, created date, last login date, and activity summary.
- `GET /api/admin/users/{userId}`
  - Returns user profile, entitlement, subscription record, credit summary, active session count, try-on summary, and recent audit entries for that user.
- `PATCH /api/admin/users/{userId}/role`
  - Allows `Free` or `Premium`.
  - Rejects `Admin` unless the user email is the fixed owner email.
  - Updates entitlement/subscription foundation records when moving between Free and Premium.
  - Writes audit log.
- `POST /api/admin/users/{userId}/credits/adjust`
  - Body includes signed integer `amount` and required `reason`.
  - Writes credit ledger and audit log.
- `GET /api/admin/audit-log?q=&action=&targetUserId=&offset=&limit=`
  - Returns recent admin actions.

Error handling:

- Invalid role changes return `400`.
- Missing target user returns `404`.
- Non-admin access returns `403`.
- Mutating admin routes require the existing CSRF header.

## Frontend UX

Use the current editorial product system:

- Warm paper and dark ink themes.
- Hairline borders and flat panels.
- Compact controls and tables.
- Crimson only for primary actions and focus states.
- No claymorphism, decorative blobs, glass, or marketing hero layout.

Shell changes:

- Account kicker shows `Free`, `Premium`, or `Admin` instead of `Studio session`.
- Admin navigation item appears only for `Admin`.
- Mobile bottom navigation includes Admin only for `Admin`; non-admin layout remains unchanged.

Admin route:

- `/admin`
- Protected by an admin route guard using `session.user.role`.
- If a non-admin manually visits `/admin`, show a restrained access-denied state. The backend still protects all admin data.

Admin page structure:

- Overview strip: users by role, total manual credit balance, try-on queue status, provider/storage health.
- Users table: search, role filter, role badge, premium state, credit balance, last login.
- User detail panel: profile, role selector for Free/Premium, credit adjustment form, entitlement/subscription summary, recent audit entries.
- Audit log table: action, target, admin, summary, timestamp.

No user-facing paywall copy, locked controls, checkout buttons, or billing screens are added outside the admin panel.

## Testing

Backend tests:

- New users default to `Free`.
- The fixed owner email resolves as `Admin`.
- `/api/auth/me` contract includes role.
- Admin endpoints reject anonymous and non-admin users.
- Admin user search returns role, entitlement, and credit summaries.
- Admin cannot assign `Admin` to another account.
- Admin can change a user between `Free` and `Premium`.
- Credit adjustment appends ledger entries and updates balance.
- Role/credit mutations write audit log entries.
- PostgreSQL schema and migrations include new admin foundation tables and indexes.
- In-memory store preserves admin foundation behavior.

Frontend tests:

- Account panel displays role instead of `Studio session`.
- Admin navigation is visible only for admin sessions.
- Admin route guard blocks non-admin sessions.
- Admin page loads overview, user table, user detail, credit adjustment, and audit data from admin APIs.
- Existing Wardrobe, Builder, and Calendar tests keep their current behavior.

Verification commands:

```powershell
dotnet run --project outfit_planner_back\tests\OutfitPlanner.Api.Tests\OutfitPlanner.Api.Tests.csproj
dotnet build outfit_planner_back\src\OutfitPlanner.Api\OutfitPlanner.Api.csproj
cd outfit_planner_front
npm test
npm run build
```

## Rollout

1. Add role and foundation data model without paywall enforcement.
2. Expose role through auth session.
3. Add protected admin API.
4. Add admin frontend route and role display.
5. Update README and `AGENTS.md` with the admin foundation boundaries.

Future Stripe integration will add provider webhook handling and Stripe checkout/customer portal flows on top of the existing subscription and entitlement records.
