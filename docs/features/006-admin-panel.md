# 006 — Admin panel and dashboard

**Status:** Done (2026-10-08 — contract §7b, backend and frontend in one change)

## Why

Running the game meant SQL by hand: promoting a player, giving test cash, resetting a
company, editing the catalogue, adding manager names. The admin panel puts all of it — and
game-wide stats — behind one admin-only screen.

## Access

- An **admin** is a player with `players.IsAdmin = true`. The access token then carries the
  role claim (`http://schemas.microsoft.com/ws/2008/06/identity/claims/role: "Admin"`).
- Dev admin account: **`admin@admin.com`**, created through `/api/auth/register` (so the
  password is BCrypt-hashed like any other) and promoted with
  `UPDATE players SET "IsAdmin" = true WHERE "Email" = 'admin@admin.com'`. Its password was
  given by the project owner and is deliberately not written in the repository.
- After the first admin, `PUT /api/admin/players/{id}/role` promotes or demotes others.
  Nobody can demote or delete themselves.

## Admins are staff, not players (same day, second pass)

An admin has **no company, no cash and no game**:

- API: the game groups (`/api/game/*`, businesses included) require the new
  `AuthPolicies.Player` policy — authenticated **and not** in the `Admin` role — so an admin
  token gets `403` there. The leaderboard and every player figure in `/api/admin/stats`
  exclude admins (filtered by owner, because a promoted player keeps their old company row).
- Frontend: two apps behind one login. `/admin` is a **top-level** route with its own
  `AdminLayoutComponent` (staff top bar: Overview / Players / Catalogue / Managers, no cash
  HUD; the phone tab bar carries the same four). The game layout route has
  `canActivate: [authGuard, playerGuard]`, so an admin opening `/dashboard`, `/businesses` or
  `/leaderboard` is sent to `/admin`, and `GameService` never starts for them (0 calls to
  `/api/game/*`). Logging in as an admin lands on `/admin`.
- The test company "Admin HQ" created for `admin@admin.com` during the first pass was
  deleted.
- Promoting a player turns them into staff from their next login; their company stays
  untouched, and demoting restores it.
- Verified: admin token → 403 on `/game/state`, `/game/company`, `/game/businesses/catalogue`,
  `/game/sync`; leaderboard without admins; headless Chrome 24/24 (admin lands on `/admin`,
  staff nav only, no HUD, sent back from all three game pages; player keeps the game and is
  kept out of `/admin`).

## Backend

- `/api/admin/*` (stats, players, manager names) — `AdminEndpoints`, all behind the existing
  `AuthPolicies.Admin` policy (no token → 401, no role → 403) and the `game-actions` rate
  limit. The catalogue editor (§7, `AdminCatalogueEndpoints`) was already there.
- `AdminService` (Application) — rules: no self-demotion/deletion, name validation, "in
  use" check before deleting a manager name (the FK is RESTRICT).
- `IAdminReadRepository` / `AdminReadRepository` — read-only projections straight into
  DTOs (stats aggregate across every player; loading aggregates would be wasteful).
- Domain: `Player.SetAdmin`, `Company.AdminSetCash`, `Company.AdminReset`, and
  **`Company.CashOverridePending`**: an admin cash change or reset sets it, and the next
  `Sync` keeps the server figure instead of the client's. Without it, an online player's
  next sync (every 5 s) reported their old, lower cash and silently undid the change —
  `/sync` only clamps figures that are too *high*.
- Migration `20261008110410_AdminCashOverride` — `companies.CashOverridePending bool NOT NULL DEFAULT false`.
- 5 domain tests (admin cash, negative cash, override wins once, reset, set admin).

## Frontend

- `AuthService.isAdmin` — read from the token's role claim (`core/http/jwt.ts`, accepts the
  full claim URI or a short `role`). UI only; the API re-checks every call.
- Route `/admin` (child of the layout) with **`canMatch: [adminMatchGuard]`** — a non-admin
  never matches it, never downloads the admin chunk, and falls through to `**` → dashboard —
  plus `canActivateChild: [adminChildGuard]`.
- "Admin" nav item (top nav + tab bar) only for admins.
- Pages under `features/admin/`: **Overview** (tiles + prestige distribution + most-owned
  businesses), **Players** (search; set cash, reset, make/remove admin, delete — with
  confirmation dialogs), **Catalogue** (list by prestige, search, retired filter; editor
  dialog for entry fields, retire/restore, add/edit/remove assets), **Manager names** (add,
  filter, delete unused).
- `GameService.syncNow` refreshes the company when the server answers `adjusted`, so a
  player reset by an admin sees it within one sync.

## Verification

- 99/99 domain tests; full solution build 0 warnings; migration applied.
- Over HTTP (real database): admin token has the role; a normal player gets 403 on
  `/api/admin/stats`, no token 401; self-demote / self-delete → 400; set cash 50,000 then the
  player's stale sync of 12 → `acceptedCash 50000, adjusted true`; negative cash → 400;
  grant admin; reset → cash 0 / TheHustle / earnings 0; add / duplicate / delete manager
  names; deleting a name in use → 400; delete player → gone.
- Headless Chrome against the real backend, logging in through the form (26/26): admin sees
  the nav item and all four pages with real data, no horizontal scroll at 390 px and
  1366 px, editor opens with assets; a normal player is sent from `/admin`,
  `/admin/players`, `/admin/catalogue` to `/dashboard` and has no Admin nav item.
- Gotcha found on the way: the `game-actions` limit (20 requests / 10 s) is **per server,
  not per user** — browsing admin pages very fast shows "Slow down — too many requests".
