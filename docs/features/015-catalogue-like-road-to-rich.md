# 015 — A catalogue that plays like Road to Rich

**Status:** Phase 1 done (catalogue, pace, reset). Phase 2 pending: daily login reward and daily
contracts. Plan and the full business list: `docs/business-catalogue-plan.html`.

## Why

The player asked for businesses that play like Road to Rich (`Desktop/Demo`) — buy delivery
vehicles and rental cars, open restaurants and level them up — while money keeps counting every
second, 6–10 businesses per prestige level, and slower growth that rewards logging in often.

## Decisions (owner, 2026-10-16)

1. **Every player is reset** when this ships: logins and companies (their names) are kept;
   cash, businesses, prestige, earnings, loans, luxury, achievements, diamonds, badges and
   avatars are deleted. Each company restarts like a new one (25 welcome diamonds).
2. Pace as in the plan's table; vehicle mileage later; daily reward and contracts as phase 2;
   the 6 new businesses as listed.

## Rules

- **Kind** on every catalogue entry and business: `Fleet` (vehicles in depots), `Sites`
  (formats such as Burger Joint / Steakhouse, levelled one by one), `Equipment` (machines in a
  place). Same engine (spec 014's places and units); the kind only changes the client's words.
- **Site formats**: `PlaceKindEntry.BaseIncomePerSecond` — each place kind earns its own income
  (null = the entry's base income). The tier multiplier still applies on top.
- **Site levels** follow Road to Rich: +25 % of the place's income per level; level n → n+1 costs
  25 % of the kind's price × 1.35^(n−1) (`GameConstants.PlaceLevel*`).
- **Pace**: every income in the catalogue is price ÷ the payback of its prestige level —
  P1 30 min, P2 1 h, P3 2 h, P4 4 h, P5 6 h, P6 10 h, P7 15 h (dearer units of a business a
  little slower, × 0.9 → × 1.1). A site earns its whole price back by itself; a depot only
  25 % of that (the vehicles earn); an equipment place 40 %. Salaries are 0 for now.
- 58 businesses: 9 at P1 and P2, 8 at P3–P7. New: Delivery Company, Bus Line, Car Dealership,
  Cargo Airline, Container Shipping, Hyperloop Network. Car Rental moved from P3 to P2.

## Backend

- `BusinessKind` enum; `Kind` on `BusinessCatalogueEntry` and `Business`; `PlaceSpec` /
  `PlaceKindEntry` carry `BaseIncomePerSecond`; place-level constants.
- Faults for every business type (office, finance, energy, aircraft, ship, space, clinic,
  showroom, kiosk, hyperloop lists, plus aliases).
- Migration `CatalogueLikeRoadToRich`: columns, the reset, then an upsert of the 58 entries with
  their place kinds and units (incomes computed from the payback table in the migration).
- DTOs: `kind` on `BusinessDto` / `BusinessCatalogueDto` / admin entry; `baseIncomePerSecond` on
  place kinds; admin requests accept both.

## Frontend

- Kind-specific words in the places panel ("Depots & vehicles", "Buy a vehicle", "Open another
  site"…), a kind badge on the catalogue page, per-day income of each place kind.
- Admin catalogue editor: kind select, place-kind income column.
- i18n: kind keys, 27 new faults, two server messages.

## Verification

- `dotnet test --solution RichLife.slnx` — 205 passed.
- Migration run on a copy of the local Aspire database: 9/9/8/8/8/8/8 businesses per level,
  every company at $0, P1, 25 diamonds, `cashOverridePending`; delivery vehicles pay back in
  27–33 minutes.
- `ng build` passes (initial bundle 35 kB over the 500 kB warning budget — dictionaries).

## Phase 2 (not built)

Daily login reward (5, 5, 10, 10, 15, 15, 30 diamonds, a missed day restarts the week) and three
daily contracts paying cash + diamonds, renewed at midnight UTC.
