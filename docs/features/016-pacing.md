# 016 — Pacing: P2 in ~4.5 days, each level a bit longer

**Status:** Done. Plan: `docs/pacing-plan.html`. Simulator: `RichLife/tools/pacing/`.

## Why

The owner wants P2 to take 3–5 days for an active player and each next level a bit longer
("not the double"). A simulator showed that an optimal player could finish the whole game in
**1.5 days** before this change.

## The bug it fixed

Business and place level-ups were priced from the opening / place price ($500 for a Delivery
Company depot) but multiply every unit inside — a $125 upgrade boosted millions in trucks and paid
back in seconds.

## Rules (constants in `GameConstants`, prices in `Company.GetPrestigeThreshold`)

- Business level: `max(openingCost, 25 % × totalValue) × 1.25^(level−1)` (`Business.NextLevelCost`).
- Place level: `25 % × (place price + its units' value) × 1.35^(level−1)` (`Business.NextLevelCostOf`).
- Each further unit of the same asset in a business × 1.2 (`Business.NextUnitPrice`); each extra
  place × 1.4 (`Business.NextPlacePrice`). The player catalogue shows the caller's next price.
- Catalogue incomes × 0.15 (P1), 0.07 (P2), 0.12 (P3), 0.15 (P4–P7) — migration `Pacing`.
- Passive income 0.1/s (was 3); new companies start with $1,000 (`GameConstants.StartingCash`,
  given by `CompanyService` — `Company.Create` defaults to 0 so domain tests are unaffected);
  `AdminReset` restores it.
- Prestige prices: $100K · $500M · $2.2B · $9B · $30B · $120B. Diamond exchange (~1 % of the next
  price) and loan amounts (~5–40 %) follow.
- Migration `Pacing` also **resets every player once more** (logins and company names kept).

## Result (simulator, active player)

P2 day 4.5 · P3 10.3 · P4 16.8 · P5 28.3 · P6 43.8 · P7 59.8. Casual (2 × 15 min a day): P2 day 10,
P7 day 156. Real players are less optimal than the simulator.

## Not changed

Managers, breakdowns, taxes, store, luxury prices (status items), business-level income bonus.
