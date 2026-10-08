# 004 — Hire managers (business automation)

**Status:** Done (2026-10-08 — contract, backend and frontend in one change)

## Why

Offline income (the "welcome back" credit) only counts **automated** businesses, but
nothing in the game could automate one — so time away only ever paid the base $3/s.
Hiring a manager is the missing idle-game loop: pay once, and that business keeps earning
while you are away (up to the 4-hour cap).

## Rule

- Price: `managerCost = 2 × openingCost` (`GameConstants.ManagerCostMultiplier`), paid in
  cash, once per business. A rule, not content — no catalogue column, no migration.
- Effect: `Business.IsAutomated = true`, so the business counts toward
  `OfflineIncomePerSecond`.
- Not refunded when the business is closed; kept through prestige (businesses are kept).
- Spending — does not reduce `AllTimeEarnings`.

## Shifts (changed the same day — supersedes "once per business" below)

A manager is no longer permanent: hiring buys a **4-hour shift** starting at the hire
(`GameConstants.ManagerShift`), then the manager leaves and the player **rehires** (same
price, new random name). The flag `businesses.IsAutomated` was replaced by
`businesses.ManagerUntil` (migration `20261008105151_ManagerShifts`, reordered by hand so
existing managers were converted to a fresh shift instead of dropped).

Time away is paid over the window `lastSyncAt` → `lastSyncAt + min(elapsed, 4 h)`: base
income for all of it, each business only for the part its shift covers
(`Business.ManagedSecondsWithin`). The player's example — hire 11:00, leave 12:00, back
18:00 — pays base 4 h + that business 3 h; tested as
`Offline_HireThenPlayOneHourThenAwaySixHours_…` and over HTTP (64,800 = 3 × 14,400 +
2 × 10,800). `isAutomated` / `offlineIncomePerSecond` in the API are "as of the response";
the frontend counts shifts down against `GameService.now` (1 s clock) via
`core/game/managers.ts` (`shiftMsLeft`, `formatShiftLeft`). No background worker is
involved — expiry is just a timestamp compared on read.

## Manager names (added the same day)

Every hired manager gets a random first name ("Lucy", "Milo", "Yasmine"…), shown in the
dialog ("Lucy runs this business") and as the badge on the dashboard card.

- Table `manager_names` (`Id` identity, `Name` unique, ≤ 40) — content, seeded with **130**
  names by migration `20261008095805_AddManagerNames` (InsertData, like the catalogue).
  Add more with a plain `INSERT INTO manager_names ("Name") VALUES (…)`.
- `businesses.ManagerNameId` → FK to `manager_names` (`RESTRICT`), plus a copied
  `ManagerName` — the same id-plus-copy shape as `CatalogueId`/`Name`.
- `IManagerNameRepository.PickRandomAsync(excludeIds)` — `ORDER BY RANDOM()`, skipping names
  the company already uses while unused ones remain. The domain receives the picked name:
  `Company.AutomateBusiness(businessId, manager)`.
- `BusinessDto.managerName` (null until hired). Frontend: `core/game/managers.ts` maps a
  name to a stable avatar emoji.
- Verified over HTTP: three managers for one company → three different names, FK id and
  copied name match in the database.

## Backend changes

- `GameConstants.ManagerCostMultiplier = 2m`; `Business.ManagerCost`.
- `Company.AutomateBusiness(businessId)` → `Result<Business>`; failures
  `"Business not found."`, `"Business already has a manager."`, `"Insufficient funds."`.
- `BusinessService.AutomateBusinessAsync`, endpoint
  `POST /api/game/businesses/{businessId:guid}/automate` → `BusinessDto`.
- `BusinessDto.ManagerCost` (new trailing field).
- 6 domain tests (`AutomateBusiness_*`). `IsAutomated` was already a persisted column.

## Frontend changes

- `BusinessDto.managerCost` in `game.models.ts`; `GameService.automateBusiness()` (syncs
  first, then deducts locally).
- Manage-business dialog: a "Hire a manager" card showing the extra offline income
  (`netIncomePerSecond × prestigeMultiplier`) and the price; after hiring it turns into
  "Manager hired · ⚡ Auto". The dashboard button reads "Manage · hire a manager" until
  hired.

## Verification

- 79/79 domain tests; full solution build 0 warnings.
- Over HTTP against the real database (throwaway player): Food Cart → `managerCost`
  1,000; hiring with $700 → `400 "Insufficient funds."`; with $1,500 → `200`, cash 500;
  hiring again → `400 "Business already has a manager."`; 1 h away →
  `offlineIncomePerSecond` 5 and `earned` 18,000 = (3 + 2) × 3,600.
- Headless browser (mocked API): the card, the purchase and the hired state render on a
  phone.
