# 009 — Bank and loans

**Status:** Done (2026-10-09 — contract §6d, backend and frontend)

## Why

Cash flow had no strategy in it: you could only wait. A loan lets a player open a business
or reach a prestige early — at a price, and with a real risk if their cash runs dry when the
bank comes to collect.

## Rules (all on the `Company` aggregate)

- **20 banks**, fictional, defined in code (`Domain/Banking/BankCatalog.cs`) — each has a
  rate band and a term band (cheap banks lend short, expensive ones long). Bank ids are
  stored on loans: **never rename or reuse one**.
- **5 offers per prestige level** (`LoanOffers.For`), the same for every player at that
  level, regenerated every **6 hours** on windows aligned to midnight UTC. Offers are never
  stored: each window has its own seed (SplitMix64, not `System.Random`, whose sequence may
  change between .NET versions). An offer id is `{window}-{level}-{slot}`; taking one
  regenerates the current set and looks the id up, so an id from a past window is
  `"This offer has expired."`.
- **Amounts follow prestige** (`GameConstants.LoanAmountRange`), log-uniform within the
  range and rounded to two significant digits:

  | Prestige | Amounts |
  |---|---|
  | P1 The Hustle | 5K – 40K |
  | P2 Small Business | 30K – 250K |
  | P3 Entrepreneur | 200K – 2M |
  | P4 Business Mogul | 2M – 20M |
  | P5 Tycoon | 20M – 200M |
  | P6 Billionaire | 200M – 2.5B |
  | P7 Global Empire | 2B – 25B |

- Interest 3–15 % (half-percent steps, per bank band); 4–16 installments.
- **One loan at a time** (aggregate check + filtered unique index
  `IX_loans_CompanyId_active` on `CompanyId WHERE RepaidAt IS NULL`).
- Taking a loan adds the amount to cash — borrowed, not earned: `AllTimeEarnings` is
  untouched; **`NetWorth` subtracts what is still owed**.
- Repay `amount × (1 + rate)` in equal installments (rounded **up** to the cent, so the last
  one is the smaller one), **one every 6 hours** from the moment the loan is taken.
- **Collection** — `Company.CollectLoanPayments(now)`, called by `/sync` (after the clamp)
  and `/state` (after offline earnings). No background job: every installment that fell due
  since the last call is collected then, in order.
- **Cash never goes negative.** A short installment takes what cash there is, the shortfall
  stays owed, and **10 %** of the shortfall is added as a penalty (`MissedPayments` + 1).
  Installments continue every 6 hours until `Outstanding` reaches 0.
- **Repay all** pays `Outstanding` from cash (collecting anything already due first); no
  early-repayment discount.
- Kept through prestige. An admin reset deletes the loans; deleting the company cascades.

## Backend

- Domain: `Banking/BankCatalog.cs`, `Banking/LoanOffers.cs` (`LoanOffer` + generator),
  `Banking/LoanCollection.cs`, `Entities/Loan.cs` (internal mutators — only `Company`
  changes a loan), `Company.TakeLoan` / `CollectLoanPayments` / `RepayLoan` / `ActiveLoan`,
  tuning in `GameConstants` (`LoanPaymentInterval`, `LoanOfferRotation`,
  `LoanOffersPerLevel`, `LoanPenaltyRate`, `LoanAmountRange`).
- Persistence: `loans` table (`LoanConfiguration`), cascade from `companies`, migration
  `BankLoans`; `CompanyRepository` includes `Loans`.
- Application: `BankService`, `BankMapper`, `BankDto` / `LoanOfferDto` / `LoanDto` /
  `LoanPaymentDto`; `SyncResultDto` and `OfflineEarningsDto` gained `LoanPayment`.
- Api: `BankEndpoints` — `GET /api/game/bank`, `POST /api/game/bank/loans/{offerId}`,
  `POST /api/game/bank/repay` (player policy, `game-actions` rate limit).
- Tests: `tests/RichLife.Tests/Domain/BankTests.cs` (22 cases: offers per level, stability
  within a window, expiry, one loan at a time, net worth, collection, multi-installment
  catch-up, shortfall + penalty with cash at 0, repay all, admin reset).

## Frontend

- `/bank` page (`features/bank/`), in the nav after Luxury (`bank` icon): active loan with
  progress and a next-payment countdown, **Repay all** with confirmation, the 5 offers with
  a "new offers in …" countdown (auto-reload at rotation), take-loan confirmation stating the
  schedule and the penalty rule, and the history of repaid loans.
- `GameService.loadBank / takeLoan / repayLoan` — take and repay **sync first**, then adopt
  the server's `cash` from the response. `GET /bank` cash is never adopted (it is the last
  recorded figure, stale against the ticker).
- A `/sync` carrying `loanPayment` shows a bank toast (paid / penalty / repaid) instead of
  the generic "balance corrected" one; `/state` shows it in the welcome-back dialog.
- All texts in `core/i18n/dict/bank.ts` (en/fr/ar); the three new server messages in
  `dict/server.ts`. Bank names are proper nouns and stay as sent.

## Verification (2026-10-09)

- `dotnet build RichLife.slnx` — 0 warnings; `dotnet test --solution RichLife.slnx` — 133
  green. `ng build` — clean, no budget warnings.
- Migration `20261009103944_BankLoans` applied to the Aspire database.
- Over HTTP with a throwaway player (deleted afterwards): 5 offers at P1 between 6.5K and
  25K; taking one credits cash; a second loan, repay-all without the cash and an old offer id
  each answer their `400`; net worth went negative by the debt. With two installments forced
  due and $117 of cash, `/sync` took the $117 (cash → 0), added a $276.27 penalty (10 % of
  the $2,762.73 shortfall), counted 2 missed payments and reported it in `loanPayment`.
