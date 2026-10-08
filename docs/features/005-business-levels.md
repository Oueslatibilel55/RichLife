# 005 — Business levels

**Status:** Done (2026-10-08 — contract, backend and frontend in one change)

## Why

Assets were the only way to grow a business. Levels add the classic idle-game second axis:
pay to level up, every level boosts the whole business (base income **and** its assets),
and milestones give big jumps worth coming back for.

## Rule

| | |
|---|---|
| Range | Level 1 (every business, including existing ones) to **100** |
| Income | `gross × levelMultiplier − salary`; salary is not scaled |
| Multiplier | `(1 + 0.10 × (level − 1)) × 2^milestones` — levels **10, 25, 50** each double it (×3.8 at 10, ×13.6 at 25, ×47.2 at 50) |
| Cost | `openingCost × 1.25^(level − 1)` for the next level — Food Cart: 500, 625, 781 … |
| Spend | Cash; not added to `totalValue` (not refunded on close); not lost earnings; kept through prestige |

Tuning lives in `GameConstants` (`MaxBusinessLevel`, `LevelIncomeBonus`, `LevelMilestones`,
`LevelCostGrowth`). The income formula lives on `Business` (`MultiplierAt`, `NetIncomePerSecond`),
so the online rate, offline accrual and the `/sync` ceiling all include levels automatically.

## Backend changes

- `Business.Level` (persisted), `LevelMultiplier`, `NextLevelCost`, `NextLevelIncomePerSecond`,
  `IsMaxLevel`; `NetIncomePerSecond` now applies the multiplier.
- `Company.LevelUpBusiness(id)`; failures `"Business not found."`,
  `"Business is already at max level."`, `"Insufficient funds."`.
- `POST /api/game/businesses/{businessId:guid}/level-up` → `BusinessDto`.
- `BusinessDto`: `level`, `levelMultiplier`, `nextLevelCost`, `nextLevelIncomePerSecond`.
- Migration `20261008102147_AddBusinessLevels` — `businesses.Level int NOT NULL DEFAULT 1`
  (the generated default of 0 was corrected to 1 so existing rows start at level 1).
- 9 domain tests.

## Frontend changes

- `GameService.levelUpBusiness(id, cost)` — sync first, deduct locally, then
  `refreshCompany()` for the new rates.
- Manage-business dialog: a level card (Lv badge, current ×multiplier, the next level's
  gain, the next milestone, "Level up · $X"). Dashboard cards show an "Lv n" chip.

## Verification

- 88/88 domain tests; full solution build 0 warnings; migration applied, existing rows at 1.
- Over HTTP against the real database (throwaway player): Food Cart level 1 → next cost
  500, next income 2.20; level-up → level 2, income 2.20, next cost 625; with no cash →
  `400 "Insufficient funds."`; up to level 10 → multiplier 3.80, income 7.60 and company
  `incomePerSecond` 10.60 (= 3 + 7.6).
- Headless browser (mocked API): the level card at 9 ("next level doubles") and at 10.
