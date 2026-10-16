# 014 — Business places, units and breakdowns

**Status:** Done (phases 1 and 2 of `docs/business-redesign-plan.html`; staff is phase 3, not built)

## Why

The design book (`Desktop/Demo/features/ideas.html`) asks for businesses with real detail: open
the same business in several places, upgrade each place, buy real things (a WashTec tunnel, a
Toyota Prius, a Speed Queen washer) — and when something breaks, that part stops earning until
it is repaired.

## Rules (contract §5 *Places and breakdowns*)

- **Places.** A business opens with its first place (the catalogue's first place kind, included in
  the opening cost). Up to 10 places; each further one costs its kind's price and earns the
  catalogue's base income × its tier (Quiet × 1, Busy × 1.5, Prime × 2.2).
- **Place levels** 1–10: level 1 → 2 costs the kind's price, then × 1.6 per level; each level adds
  `placeCapacityPerLevel` room and +5 % to that place. Business levels still apply on top.
- **Units** (the catalogue's assets) stand in a place, up to its capacity.
- **Breakdowns.** A unit's next failure (`breaksAt` = from + MTBF × [0.5, 1.5)) and its fault are
  drawn with SplitMix64 from the unit id and its repair count — never stored, never rolled by the
  client. Broken = earns nothing. Faults are rules in code: `Domain/Operations/FaultCatalog.cs`
  (fault ids are part of the contract and translated by the client — never rename or reuse one).
- **Repair** (fault price, back after its repair time), **fast repair** (× 2.5, at once),
  **repair all** (standard, all or nothing).
- **Managers repair** during their shift, at the standard price, only when cash covers it:
  offline (`Company.ApplyOfflineRun`, at the moment of the failure, so the unit earns again once
  fixed) and online (`Company.RunManagerRepairs` on `/sync` and `/state`).
- The sync ceiling counts every unit as working (breaks only lower income).

## Decisions taken (plan §9)

1. Business level kept; place level added on top.
2. Managers may spend cash on repairs, never into the red.
3. Broken units earn 0 %; MTBF 30–200 h per unit type (most 36–96 h).
4. Phases 1 + 2 shipped together; staff (workers per unit, salary per place) left for later.
5. **Gas Stations** became the existing admin-made `fuel-station` entry (Small Business, $70k),
   which already sold a pump, a car-wash bay and a shop: it received the book's places and an EV
   charger and a café, instead of a second fuel business.

## Backend

- Domain: `BusinessPlace`, `BusinessAsset` (now a unit: place, MTBF, `WorkingFrom`, `BreaksAt`,
  `RepairCount`), `PlaceKindEntry` in the catalogue aggregate, `PlaceTier` / `UnitStatus` enums,
  `Common/SeededRandom` (shared with the loan offers), `Operations/FaultCatalog`, `StreetNames`.
  `Business` income is time-dependent: `NetIncomePerSecondAt(t)` / `FullNetIncomePerSecond`;
  `Company.IncomePerSecondAt(t)` / `FullIncomePerSecond`.
- Routes: open place, level place, buy unit into a place, repair (`?fast=`), repair all; admin
  place-kind CRUD. `/sync` and `/state` report `autoRepairs` / `repairsPaid`.
- Migration `BusinessPlacesAndUnits`: tables `business_places`, `catalogue_place_kinds`; unit and
  catalogue columns; content for the 17 seeded businesses + `fuel-station` (inserted only where the
  entry exists; only the original seed assets are replaced — admin-added ones stay); every owned
  business gets its first place holding all its units with unchanged income; first failures spread
  0.5–1.5 × MTBF after the migration. Drops `businesses.GrossIncomePerSecond`.
- Tests: `PlacesAndBreakdownsTests` (20).

## Frontend

- `core/game/units.ts`: unit status on the client clock; `companyIncomeNow` corrects the ticker
  rate every second for units that broke or came back.
- `features/dashboard/places/`: the places panel inside the manage dialog (place cards, units with
  OK / fault / back-in, repair and fast repair, repair all, upgrade, add a unit, open a place).
- Dashboard cards: live income and a 🔧 broken badge. Repair toast and welcome-back line.
- Admin catalogue editor: place kinds, unit MTBF, place capacities.
- i18n: `dict/places.ts` (UI, tiers, 54 faults), new server messages in `dict/server.ts`.

## Verification

- `dotnet test --solution RichLife.slnx` — 204 passed.
- Migration run on a copy of the local Aspire database: an owned car wash kept its income
  (place base 5/s + its 3 assets), first failures 1.6–2.8 days out.
- API run against the migrated copy: open place, buy unit, full place, place level, broken unit
  (income 22.55 → 19.25/s), standard / fast repair, manager auto-repair on `/sync`.
- `ng build` passes; checked in the browser: places panel, broken unit, fast repair, welcome-back
  repair line. The initial bundle is now 26.5 kB over the 500 kB warning budget (dictionaries are
  loaded eagerly — the admin dictionary alone is 44 kB and could be lazy).

## Not done (later)

Staff per place, weather and events, theft / inspections / stock-outs, vehicle mileage and the
1,000,000 km scrap, new vs used vehicles, airport ↔ parking links.
