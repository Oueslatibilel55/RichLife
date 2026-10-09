# 011 — Diamonds and the store

**Status:** Done (2026-10-09 — contract §6e and the fields it adds to §2, §4, §6, §6b, §7b; backend, frontend, admin)

## Why

A second, rare currency gives players short-term goals beyond cash, and a reason to watch ads
once ads exist. It is spent on things that save time or show status, so the leaderboard stays
winnable without it (roadmap → "Diamonds (premium currency)").

## Rules (all in code — `GameConstants`, `Domain/Store/BadgeCatalog`)

- **Balance**: a whole number on the company. The server is the only source of truth; every gain
  and spend writes a line to `diamond_transactions` (amount, balance after, reason, detail).
- **Earned**: 25 when the company is created; 10 per achievement unlocked; 20 × the new level's
  number at each prestige (P2 → 40 … P7 → 140); admins can give or take away (with a note).
  Ads are a later source. **Cash can never be turned into diamonds**, and they cannot be bought.
- **Income boost**: 1 h / 3 h / 8 h for 25 / 60 / 140 💎. Every income × 2 until `BoostUntil` —
  online and offline (`ApplyOfflineProgress` pays the boosted part of the window again) — and the
  `/sync` ceiling counts boosted seconds twice. Buying while one runs adds time; at most 24 h
  ahead. `incomePerSecond` in the API never includes the boost: the client multiplies its ticker.
- **Double offline earnings**: after `/state` credits time away, its earnings can be paid again
  for 15 💎, once, within 30 minutes (`OfflineBonusAmount` / `OfflineBonusUntil`). Counts as
  earnings.
- **Exchange**: n 💎 → n × `DiamondCashValue(prestige)` cash (250 · 2,000 · 20,000 · 200,000 ·
  2.5 M · 30 M · 300 M — 1 % of the next prestige price). **Not** earnings (leaderboard untouched).
- **Badges**: 16 in four rarities (common 20, rare 50, epic 120, legendary 250 💎), one of each,
  kept forever. The first bought becomes the featured badge, shown next to the name on the
  profile and the leaderboard; the player can change or hide it. Badge ids are stored: never
  rename or reuse one.
- **Admin reset** keeps diamonds and badges (they may one day be paid for) but stops a boost and
  cancels the double offer.

## Backend

- Domain: `Company.Diamonds`, `BoostUntil`, `OfflineBonusAmount/Until`, `FeaturedBadgeId`,
  owned `Badges` (`company_badges`), `DiamondLedger` (new lines only — the ledger is never
  loaded); `BuyBoost`, `OfferOfflineDouble`, `DoubleOfflineEarnings`, `ExchangeDiamonds`,
  `BuyBadge`, `FeatureBadge`, `AdminAdjustDiamonds`; grants inside `Create`,
  `UnlockAchievements` and `Prestige`. All balance changes go through one private `Record`.
- `Domain/Store/BadgeCatalog.cs` (badges + `DiamondReasons`), `Entities/DiamondTransaction.cs`,
  `Entities/CompanyBadge.cs`.
- Application: `StoreService` (`/api/game/store`), `StoreDto` & co; `/state` sets the double
  offer; `/sync` returns `diamonds` and the diamonds of each new achievement; profile, leaderboard
  (`badgeIcon`) and admin stats / players extended; `AdminService.AdjustDiamondsAsync`.
- Infrastructure: `DiamondTransactionConfiguration`, `CompanyRepository.GetDiamondHistoryAsync`.
- Migration `DiamondsAndStore`: columns, `company_badges`, `diamond_transactions`, and a
  **backfill** — existing companies get 25 + 10 × achievements already unlocked, with ledger lines
  (`welcome`, `backfill`).
- Tests: `StoreTests` (21) — **167** green.

## Frontend

- **Navigation redesigned**:
  - Desktop top nav: all 7 pages, with labels from 1280 px and icons with a tooltip between 900 and 1279 px.
  - Phone tab bar: Home · Businesses · **Store** (raised, centre) · Bank · **More**.
  - "More" is a bottom sheet with Luxury, Leaderboard, Profile, the language picker and log out. On phones the top bar keeps only the money.
- **Top bar**: a 💎 chip linking to the store, and while a boost runs an amber rate chip (⚡) plus an "×2 · 42:10" countdown.
- **Welcome back**: a "Double it · 15 💎" button, disabled with a hint when the player lacks diamonds.
- **Achievement toasts** show "+10 💎".
- **`/store`** has tabs:
  - Boosts: three offers with their estimated gain.
  - Badges: grid by rarity, with buy and show/hide.
  - Exchange: rate, 10/25/50/All, preview, the one-way note.
  - History: the last 20 movements, with translated reasons.
- **Profile**: the featured badge next to the name, a Diamonds stat and a Badges strip. **Leaderboard**: the featured badge after the name.
- **Admin**:
  - Players: a Diamonds stat and a 💎 action (amount ±, reason).
  - Overview: diamonds held / earned / spent, badges owned, boosts running, and badges bought per badge.
- `GameService`: `diamonds`, `boosted`, `boostMsLeft`, `store`; `cashRate` × the boost; store actions sync first and adopt the answer's cash, diamonds and boost (featuring a badge does not sync and does not adopt cash).
- i18n: `dict/store.ts` (en/fr/ar, badge names `badge.<id>`), new server messages in `dict/server.ts`.

## Verification (2026-10-09)

- Backend: 167 tests green; migration applied to the Aspire database, backfill checked
  (35 = 25 + one achievement).
- Over HTTP with a temporary player and a temporary admin (both deleted afterwards):
  - Every store error message in the contract.
  - Boost 1 h → `boostUntil` +1 h and 25 💎 spent.
  - `/state` returns the double offer, and doubling works.
  - Badge bought and featured, shown on the profile and as `badgeIcon` on the leaderboard.
  - Exchange 10 💎 → $2,500 with `allTimeEarnings` unchanged; the ledger history is in order.
  - Admin: ±diamonds validation and +100 with a note; the stats carry the new figures.
- Frontend: production build clean (no budget warnings); i18n parity 0 problems. Not checked in a browser.

Avatars (profile pictures) were added to the store afterwards — see [012](012-avatars.md).

## Later

- Rewarded ads as a source (with server-side verification by the ad network).
- Badges or boost prices editable by admins (today rules in code).
