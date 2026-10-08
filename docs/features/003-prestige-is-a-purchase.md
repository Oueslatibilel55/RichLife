# 003 — Prestige is a purchase, not a reset

**Status:** Done (2026-10-08 — contract, backend and frontend in one change)

## Why

Prestige used to be a **full reset**: cash to 0 and every business surrendered. A player
holding $400,000 who prestiged at a $200,000 threshold lost all $400,000 and every business
— which players read as a bug. Prestige is now something you **buy**: the price comes out
of cash, and you keep the rest of your cash and all your businesses.

## Rule

| | Before | After |
|---|---|---|
| Gate | `NetWorth >= threshold` | `Cash >= price` — business value cannot pay |
| Cash | → 0 | `cash − price` |
| Businesses and their assets | all removed | **kept** |
| `prestigeLevel` / `prestigeCount` / multiplier | +1 | +1 (unchanged) |
| `passiveIncomePerSecond` | `3 × newMultiplier` | `3 × newMultiplier` (unchanged) |
| `allTimeEarnings` | kept | kept (spending is not lost earnings) |
| `400` message | `"Net worth must reach $200,000 to prestige."` | `"Prestige costs $200,000 in cash."` |

Prices are the old thresholds, unchanged and still in code (`Company.GetPrestigeThreshold`):
25k → 200k → 2M → 20M → 250M → 3B. The wire field keeps its name, `nextPrestigeThreshold`;
only its meaning changed (a cash price).

## Backend changes

- `Domain/Entities/Company.cs` — `CanPrestige` compares `Cash`, not `NetWorth`; `Prestige`
  calls `DeductCash(price)` and no longer clears `_businesses` or zeroes `Cash`. The
  message is built with `CultureInfo.InvariantCulture`: the old `:C0` printed
  "25 000 €" on a French-locale machine.
- `tests/…/CompanyTests.cs` — the reset tests were replaced:
  `CanPrestige_DoesNotCountBusinessValue`,
  `Prestige_DeductsThePrice_KeepsTheRest_AdvancesLevel_AndRaisesEvent`,
  `Prestige_KeepsBusinessesAndTheirAssets`, `Prestige_PreservesAllTimeEarnings`.
- **Not needed:** no migration, no new endpoint, no DTO change.

## Frontend changes

- `core/services/game.service.ts` — `canPrestige`, `neededForPrestige` and
  `prestigeProgress` use the live `cash` instead of `netWorth`.
- `features/dashboard` — the prestige card says "Costs $X in cash · keep your businesses";
  the button opens a **confirmation dialog** (cash now, price, cash after, multiplier
  before → after, businesses kept). Nothing is sent until "Confirm prestige".

## Data

All three dev players were reset to a fresh start on the same day (cash 0, P1, ×1.00,
$3/s, all-time earnings 0, no businesses) so nobody kept an advantage from the old rule.
A data-only `pg_dump` of the game tables was taken first.

## Verification

- 73/73 domain tests green.
- Over HTTP against the real database, with a throwaway player: cash 40,000 → open Food
  Cart (500) → prestige → cash **14,500**, the business still there, `SmallBusiness`,
  ×1.18. A second prestige → `400 "Prestige costs $200,000 in cash."`.
- Headless browser (mocked API): the dialog opens without sending anything, Cancel
  closes it with no request, Confirm sends exactly one `POST /api/game/prestige`.
