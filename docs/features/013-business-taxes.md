# 013 — Business taxes

**Status:** Done (2026-10-09 — contract §5 *Taxes*, plus tax fields on §2 `CompanyDto` / `BusinessDto`, §4 `/state`, `/sync`, `/prestige`, §6b profile, §7b admin; backend, frontend, admin)

## Why

Income only ever piled up. A recurring bill per business gives cash a sink that scales with
success, and makes players come back to settle it — with a real consequence (no prestige) for
ignoring it, but never a negative balance.

## Rules (in code — `GameConstants.TaxRate` / `TaxPeriod`, `Business`, `Company`)

- **7 %** of what each business **earned** during each **24-hour period**, counted from when it
  was opened. Businesses that existed before this feature start their first period at the
  migration (nothing earned before is taxed).
- **What a business earned**:
  - Online: each `/sync` splits the credited total between base income and the businesses by
    their `NetIncomePerSecond` (prestige and boost multipliers apply to all alike).
  - Offline: what it earned during its manager's shift, computed exactly in `ApplyOfflineProgress`.
  - A business losing money earns nothing taxable.
  - Not taxed: base income, the double-offline bonus, the diamond exchange, loans and refunds.
- **Billing**: when a period ends, its 7 % (rounded to cents) is added to the business's
  `TaxDue` by the next `/sync`, `/state`, `/prestige` or pay call. Several periods missed while
  away make one bill, and the period start jumps to the current window.
- **Never automatic**: billing never touches cash, so cash never goes negative. Unpaid bills
  **add up**.
- **Paying**: one business, or all at once. It is the whole bill or nothing (`"Insufficient funds."`).
  It is a spend, not lost earnings — `AllTimeEarnings` is untouched.
- **Consequences of not paying**:
  - Prestige is refused: `"Pay your taxes before prestige."`.
  - That business can't be closed: `"Pay this business's taxes before closing it."` — otherwise
    closing would escape the bill.
  - `NetWorth` subtracts `TaxesDue`.
- `Company.TaxesPaid` keeps the lifetime total. An admin reset clears it (the businesses and
  their bills go too).

## API

- `POST /api/game/businesses/{businessId}/pay-taxes` pays one business.
- `POST /api/game/businesses/taxes/pay` pays all of them. It uses two segments so it never
  shadows a catalogue id.
- Both answer with `TaxPaymentDto { paid, cash, company }`. Errors: `"No taxes due."`,
  `"Business not found."`, `"Insufficient funds."`.
- New fields:
  - `BusinessDto`: `taxDue`, `taxAccruing`, `taxPeriodEndsAt`.
  - `CompanyDto`: `taxesDue`, `taxRate`.
  - `/sync`: `taxesDue`, `taxBilled`. `/state`: `taxBilled`.
  - Profile company: `taxesDue`, `taxesPaid`.
  - Admin stats: `taxesDue`, `taxesPaid`, `companiesOwingTaxes`. Admin player row: `taxesDue`.

## Backend

- `Business`:
  - Stored: `TaxPeriodStart`, `TaxableEarnings`, `TaxDue`.
  - Computed: `TaxPeriodEndsAt`, `TaxAccruing`.
  - Internal: `RecordEarnings`, `AssessTaxes`, `PayTaxes`.
- `Company`:
  - `TaxesPaid`, `TaxesDue`.
  - `AssessTaxes(now)`, `PayBusinessTaxes`, `PayAllTaxes`.
  - Earnings are recorded in `Sync` (`RecordOnlineBusinessEarnings`) and `ApplyOfflineProgress`.
  - Guards are added to `CanPrestige` and `CloseBusiness`.
- Wiring:
  - `CompanyService` bills on `/state`, `/sync` and `/prestige`.
  - `BusinessService.PayTaxesAsync` / `PayAllTaxesAsync` bill first, then pay.
- Migration `BusinessTaxes` adds 3 business columns and 1 company column, and backfills `TaxPeriodStart = now()`.
- Tests: `TaxTests` (14), **185** green.

## Frontend

- `GameService`:
  - `taxesDue`; `canPrestige` also needs `taxesDue === 0`.
  - `payTaxes(id)` and `payAllTaxes()` sync first, then adopt `cash` and `company`.
  - A `/sync` or `/state` with `taxBilled > 0` raises `taxToast` (6 s) and refreshes the company.
- **Dashboard**:
  - Each business card has a tax line: due + Pay, or "tax so far · billed in 5 h 12 m".
  - A red banner shows "Taxes due" with **Pay all**.
  - The prestige button reads "Pay your taxes first" while any are due.
- **Manage dialog**:
  - A tax card shows what is due, the rate, the next bill and what has built up so far, with a Pay button.
  - Closing is replaced by a hint while tax is due.
- **Top bar**: a red 🧾 chip with the amount due, linking to the dashboard.
- **Profile**: "Taxes paid", with the amount due when there is one.
- **Admin**:
  - Overview: an "unpaid taxes" tile (paid total, companies owing).
  - Players: an unpaid-taxes stat.
- i18n: `dict/taxes.ts` (en/fr/ar); 3 server messages in `dict/server.ts`. Styles are global in
  `styles.scss` (`.tax-*`), since the component sheets are at their budget.

## Verification (2026-10-09)

- **Backend:** 185 tests green; migration applied to the Aspire database.
- **Over HTTP, with a temporary player.** A business's period was moved back 25 h in SQL so no waiting was needed.
  - `/sync` billed $70 on $1,000 earned and left cash untouched.
  - The company, the business and the profile show the bill.
  - Prestige and closing are refused with the contract messages.
  - Paying one business works. Paying again, or paying all with nothing due, answers `"No taxes due."`. An unknown id answers `"Business not found."`.
  - A second bill of $140 was refused with `"Insufficient funds."` when cash was short, then paid with Pay all.
  - Prestige then succeeds, and `taxesPaid` is 210.
- **Admin:** stats and the player row carry the new fields (checked with a temporary admin).
- **Cleanup:** both temporary accounts were deleted.
- **Frontend:** production build clean; i18n parity 0 problems. Not checked in a browser.

## Later

- A tax-paying achievement, or a late-payment penalty (like the bank's), if bills are ignored.
- Sector-specific rates, or deductions (salaries), as admin-editable content.
