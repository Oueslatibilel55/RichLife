# 001 — Expose `allTimeEarnings` on `CompanyDto`

**Status:** Done (2026-10-09 — backend 2026-10-01, frontend follow-up below completed)
**Raised:** 2026-10-01
**Touches:** backend (2 lines), frontend (dashboard tile)

---

## Why

The dashboard shows **Cash** and **Net Worth**, but not the number that actually decides
a player's rank. All three mean different things and players confuse them:

| Number | Meaning | Can it decrease? |
|---|---|---|
| `cash` | Spendable balance | Yes — spending |
| `netWorth` | `cash` + liquidation value of everything owned | Only via the cap/clamp |
| `allTimeEarnings` | Every dollar income has ever produced, cumulative | **Never** |

`allTimeEarnings` is what `/api/leaderboard` ranks on, and it is the only one of the
three that **survives prestige** — `Company.Prestige()` spends the prestige price out of
`Cash` (since 2026-10-08 it no longer zeroes cash or clears businesses — see
[003](003-prestige-is-a-purchase.md)) but leaves `AllTimeEarnings` untouched, so
prestiging does not cost you your rank. Showing it on the dashboard tells the player where they stand without a
trip to the leaderboard.

## The problem

`Company.AllTimeEarnings` already exists in the domain, is already persisted, and already
has a dedicated index (`IX_companies_AllTimeEarnings`, added in
`20260930130802_FixEconomyRulesAndAuditColumns`). It is simply **never mapped into
`CompanyDto`**, so the frontend has no way to read its own value — `/api/leaderboard`
only returns the top N players, so a player outside that page cannot find their own
figure.

---

## Backend changes

### 1. `src/RichLife.Application/DTOs/CompanyDto.cs`

Add one positional parameter to the `CompanyDto` record, **immediately after `NetWorth`**:

```csharp
public record CompanyDto(
    Guid Id,
    string Name,
    decimal Cash,
    decimal PassiveIncomePerSecond,
    decimal IncomePerSecond,
    decimal OfflineIncomePerSecond,
    decimal NetWorth,
    decimal AllTimeEarnings,   // <-- add
    PrestigeLevel PrestigeLevel,
    int PrestigeCount,
    decimal PrestigeMultiplier,
    decimal NextPrestigeThreshold,
    DateTime LastSyncAt,
    IReadOnlyList<BusinessDto> Businesses
);
```

### 2. `src/RichLife.Application/Mapping/CompanyMapper.cs`

Add the matching argument in `ToDto(Company c)`, in the **same position** — the record is
positional, so the order of the two files must agree:

```csharp
public static CompanyDto ToDto(Company c) => new(
    c.Id,
    c.Name,
    c.Cash,
    c.PassiveIncomePerSecond,
    c.IncomePerSecond,
    c.OfflineIncomePerSecond,
    c.NetWorth,
    c.AllTimeEarnings,         // <-- add
    c.PrestigeLevel,
    c.PrestigeCount,
    c.PrestigeMultiplier,
    c.GetPrestigeThreshold(),
    c.LastSyncAt,
    c.Businesses.Select(ToDto).ToList());
```

`CompanyMapper.ToDto(Company)` is the **only** construction site for `CompanyDto` —
verified across `src/` and `tests/`. Nothing else needs touching.

### Explicitly NOT needed

- **No migration.** `AllTimeEarnings` is already a persisted column
  (`numeric(20,4)`), present since `20260702103537_InitialCreate`.
- **No domain change.** `Company.AllTimeEarnings` is already a public property with a
  private setter, maintained by `AddCash` and `Sync`.
- **No new endpoint.** Every endpoint that returns a `CompanyDto` picks this up for free:
  `POST /api/game/company`, `GET /api/game/company`, `GET /api/game/state` (nested under
  `company`) and `POST /api/game/prestige`.
- **No change to `/api/leaderboard`.** It already returns `allTimeEarnings`.
- **No serializer config.** camelCase and `decimal` → JSON number are already the
  defaults.

### Backend verification

```bash
cd RichLife && dotnet test --solution RichLife.slnx
```

Then, with a bearer token, confirm the field is on the wire and non-zero after some
income has been synced:

```bash
curl -s http://localhost:5187/api/game/company -H "Authorization: Bearer $TOKEN"
# expect: ... "netWorth": …, "allTimeEarnings": …, "prestigeLevel": …
```

Sanity checks worth running by hand:

- It **never decreases.** Open a business, then close one — `allTimeEarnings` must not
  move for either (a refund returns capital; it is not earnings).
- It **survives prestige.** Note the value, prestige, read it again: unchanged, while
  `cash` has dropped by the prestige price.

---

## Frontend changes

**Already implemented** — it degrades gracefully, so it can ship before or after the
backend.

- `src/app/core/models/game.models.ts` — `allTimeEarnings?: number` on `CompanyDto`,
  typed **optional** precisely because the backend does not send it yet.
- `src/app/features/dashboard/dashboard.component.html` — an "All-Time Earned" mini-stat
  immediately to the **left** of Net Worth, wrapped in `@if (company.allTimeEarnings !== undefined)`.
  While the field is absent the tile simply does not render; the moment the backend ships
  it, it appears with no frontend deploy.

### Follow-up once the backend has shipped — done 2026-10-09

1. Make the field **required** in `game.models.ts` (drop the `?`).
2. Drop the `@if` guard in the dashboard template.
3. Remove the ⏳ marker from the `allTimeEarnings` row in `docs/api-contract.md` §2.
4. Set this spec's status to `Done` and update the index in `features/README.md`.

### Frontend verification

With the backend field live, on `/dashboard`:

- The tile reads the same figure the player shows on `/leaderboard`.
- It does not tick upward in real time — unlike cash, it only moves when `/sync` lands
  (every 5 s), because `AllTimeEarnings` grows server-side inside `Company.Sync`.
- Opening a business drops **cash** but leaves all-time earnings flat.
