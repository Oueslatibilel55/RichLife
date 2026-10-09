# 010 — Admin catch-up: luxury, bank, achievements, levels

**Status:** Done (2026-10-09 — contract §7b, §7c, §7d, backend and frontend)

> Diamonds and badges in the admin panel (players: give/take diamonds; overview: diamond, badge and boost figures) came with [011](011-diamonds-and-store.md).

## Why

Between 2026-10-08 and 2026-10-09 the game gained managers, business levels, achievements,
a luxury collection, languages and a bank. The admin panel (006) predates most of them:
admins could not edit the 76 luxury items, see who owes the bank, help a player stuck in
debt, or see how the new systems are used.

## What admins can do now

| Area | Before | Now |
|---|---|---|
| Luxury items | Seeded by migration, no editing | **Editor** `/admin/luxury`: list (thumbnail, owners, retired badge), create, edit, retire/restore — §7d |
| Bank | Nothing | **Loans page** `/admin/loans`: active / all loans with debt, penalties, missed payments, next payment; **forgive** a loan; the 20 banks (read-only) with usage — §7c |
| Players | Cash, prestige, businesses | + highest business level, luxury owned, achievements, **debt**; **Forgive loan** action — §7b |
| Overview | Players, cash, businesses, managers, levels | + luxury owned and catalogue size, achievements unlocked and per-achievement counts, loans taken / active / outstanding / missed payments — §7b |

Deliberately **not** editable:

- **Banks and offer rules** (rates, amounts per prestige, 6-hour cycle, 10 % penalty) and
  **achievements** — rules in code (`Domain/Banking`, `Domain/Achievements`), like
  `GameConstants`. Changing them is a code change and a deploy, so they cannot drift per
  environment. The admin panel shows them read-only.
- **Languages** — nothing server-side to manage; the language is a per-browser choice.
- **Luxury photos** — files on the frontend origin (`public/luxury/`). An item can only point
  at a photo that is deployed; the credit and source must stay with it (licenses).

## Backend

- Domain: `LuxuryCatalogueEntry.CreateNew` / `Update` with validation (slug id ≤ 60, name ≤ 80,
  description ≤ 300, price > 0, image URL `/…` or `https://…` ≤ 300, credit ≤ 200, source
  ≤ 500, known category and prestige) and `LuxuryItemDetails`; `Company.AdminForgiveLoan` →
  `Loan.Forgive` stores `ForgivenAmount` (outstanding becomes 0, the loan closes, cash is
  untouched). Migration `LoanForgiveness` (one column, default 0).
- Application: `LuxuryAdminService`; `AdminService.ForgiveLoanAsync` / `GetLoansAsync` /
  `GetBanksAsync` (banks from `BankCatalog` + usage from the loans table); new DTOs
  (`AdminLoanDto`, `AdminBankDto`, `AdminLuxuryItemDto`, `AchievementCountDto`, requests);
  `AdminStatsDto` and `AdminPlayerDto` extended; `LoanDto.Forgiven`.
- Infrastructure: `AdminReadRepository` (stats, player projection, loans, bank usage, luxury
  owners — loan `Outstanding` is spelled out in the queries so it runs in SQL);
  `LuxuryCatalogueRepository.GetAllAsync / GetForUpdateAsync / AddAsync`; `DbSet<Loan>`.
- Api: `AdminEndpoints` (`/players/{id}/forgive-loan`, `/loans`, `/banks`),
  `AdminLuxuryEndpoints` (`/api/admin/luxury`). All behind `AuthPolicies.Admin`.
- Tests: forgive-loan (2) in `BankTests`, `LuxuryCatalogueEntryTests` (11) — **146** green.

## Frontend

Admin panel (`features/admin/`): overview tiles, players columns + forgive action, new
**Loans** and **Luxury** (list + editor) pages in the admin nav. Player bank history shows a
"Forgiven" badge. All texts in en/fr/ar; the new server messages in `dict/server.ts`.

## Verification (2026-10-09)

Over HTTP against the Aspire database with a temporary admin and a temporary indebted player
(both deleted afterwards, plus a temporary luxury item): stats carry the new figures; the
player row shows `loanOutstanding`; `/admin/loans` lists the loan; `/admin/banks` returns 20
banks; forgive clears the debt and a second forgive answers `"Player has no active loan."`;
the player's own bank history then shows `forgiven: true`, outstanding 0; the luxury editor
lists 76 items, rejects a bad slug, creates, edits and retires, and answers 404 for an unknown
id; a player token gets 403 on `/api/admin/loans`.
