# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
ng serve                 # dev server on http://localhost:4200 (uses the `development` config)
ng build                 # PRODUCTION build by default (defaultConfiguration: production)
ng build --configuration development
ng test                  # Karma + Jasmine
ng test --include='**/some.component.spec.ts'   # single spec file
```

There are currently **no `.spec.ts` files** in the repo, and `angular.json` sets `skipTests: true` for every
schematic (component, service, guard, interceptor, …). `ng test` runs an empty suite; use
`ng generate component x --skip-tests=false` if a spec is wanted.

### Local environment caveat

Node and npm are not on the shell `PATH` here; Node lives at `C:\Program Files\nodejs\node.exe` and is
**v18.18.0**, below the v18.19 minimum the Angular 19 CLI enforces — it refuses to run at all. Either upgrade
Node, or fetch a portable build and invoke the local CLI with it:

```bash
node.exe node_modules/@angular/cli/bin/ng.js <cmd>
```

Don't claim a build passed without actually running it.

## Backend dependency

This is the frontend half of the RichLife monorepo (one git repository; the root `../CLAUDE.md` covers
cross-boundary work). The API is the sibling .NET solution at `../RichLife` (it has its own CLAUDE.md),
served on `http://localhost:5187`.

**`../docs/api-contract.md` is the source of truth** for every route, payload and status code, and §9
records frontend conformance plus the deliberate client-side behaviours. Read it before touching anything
that talks to the API; when the two disagree, the contract wins.

Environments: `environment.ts` is the **production** default (`production: true`, `apiUrl: '/api'` —
same-origin, i.e. behind a reverse proxy). `environment.development.ts` replaces it in the `development`
build config only. It is **also** same-origin `/api`: `ng serve` proxies `/api` to `http://localhost:5187`
(`proxy.conf.json`, wired in `angular.json` → serve → development). That is what lets a single tunnel on
port 4200 serve the app to a phone or another PC; the dev server only answers the tunnel domains listed in
`allowedHosts` there. The proxy only exists under `ng serve` — a static `ng build --configuration development`
output has no `/api` behind it.

Every documented endpoint now has a caller except `POST /api/game/sync-beacon`, which `sendBeacon` cannot
authenticate (M1 in the contract, resolved by a keepalive `fetch` to `/game/sync` instead).

## Architecture

Angular 19 standalone application — no NgModules anywhere. Bootstrapped in `src/main.ts` with
`appConfig` (`src/app/app.config.ts`), which wires the router, `provideHttpClient(withInterceptors([authInterceptor]))`,
and zone-based change detection with event coalescing. State is held in **signals**, not RxJS subjects;
RxJS appears only as `HttpClient` observables.

```
core/
  models/      game.models.ts, auth.models.ts  — wire contracts, mirror api-contract.md §2
  game/        prestige.ts, sectors.ts, format.ts, managers.ts, taxes.ts, countries.ts (all 249 ISO 3166-1 codes) — shared constants and pure formatters
  http/        api-error.ts — maps a failure to a displayable message; jwt.ts — reads token claims (UI only)
  guards/      authGuard, guestGuard, admin.guard (adminMatchGuard, adminChildGuard, playerGuard)
  interceptors/auth.interceptor.ts
  services/    auth.service.ts, game.service.ts
  i18n/        i18n.ts, translate.pipe.ts, translated-title.strategy.ts, dict/<feature>.ts (en/fr/ar)
features/      auth/{login,register}, dashboard (+ manage-business), businesses, luxury (+ luxury.service, luxury.models),
               bank, store, leaderboard, profile (+ profile.service, profile.models),
               admin/ (admin-layout, overview, players, catalogue (+ editor), managers, loans, luxury (+ editor);
                       admin.service, admin.models)
shared/
  components/layout  — persistent HUD/nav shell (top bar + mobile tab bar)
  components/icon    — inline SVG icon set
  components/avatar  — <app-avatar [avatar] [name] [size]>: the player's avatar (emoji on its gradient) or initial
  components/lang-switcher — <app-lang-switcher />: en / fr / ar picker
  pipes/money.pipe.ts — `money` (whole dollars) and `rate` (keeps cents)
```

`AppComponent` is just `<router-outlet />`. **Every component is `ChangeDetectionStrategy.OnPush`** — the
50 ms ticker would otherwise run a full application-wide change-detection pass 20× a second.

### Routing (`app.routes.ts`)

Every route is lazy via `loadComponent`, and each has a `title`. `/login` and `/register` are standalone pages
behind `guestGuard`; everything else is a child of a pathless route that loads `LayoutComponent` behind
`authGuard`. Adding a protected page means adding a child under that route, not a top-level one. `authGuard`
returns a `UrlTree` carrying a `returnUrl`, which `LoginComponent` honours.

**Two apps behind one login** (`features/006-admin-panel.md`): admins are staff, not players.
- `/admin` is a **top-level** route loading `AdminLayoutComponent` (staff top bar + phone tab bar: Overview,
  Players, Catalogue, Managers, Loans, Luxury; no cash HUD; section heading from each child route's `data`;
  between 900 and 1199 px the top nav drops its icons and the brand text so six sections fit). Guarded by
  `canMatch: [adminMatchGuard]` — a non-admin never matches it (nor downloads its chunk) and falls through to
  the game — plus `canActivateChild: [adminChildGuard]`.
- The game layout route has `canActivate: [authGuard, playerGuard]`: an admin is sent to `/admin`, so
  `LayoutComponent` — and with it `GameService`'s bootstrap, ticker and sync — never runs for an admin.
  After login a user goes to `returnUrl` (default `/dashboard`; register always goes to `/dashboard`);
  `playerGuard` forwards admins on to `/admin`.
- All guards read `AuthService.isAdmin`, a computed over the access token's role claim (`core/http/jwt.ts`
  accepts the full .NET claim URI or `role`). UX only — the API answers 403 to admins on `/api/game/*` and to
  players on `/api/admin/*`. A role change shows up after the next login/refresh (new token).
- Admin pages call `AdminService` (stateless HTTP); keep their requests modest — the server's `game-actions`
  rate limit (20 req / 10 s) is one budget per signed-in user, shared by every page they open.
- Admin pages (contract §7–§7d): **Overview** (stats incl. luxury, achievement distribution — titles via
  `ach.<code>.title`, server English as fallback — and loan totals); **Players** (cash, reset, role, delete,
  and **Forgive loan** when `loanOutstanding` is not null — `POST /admin/players/{id}/forgive-loan`, same
  in-page confirmation modal, the returned row replaces the old one); **Catalogue** (business editor in a
  modal); **Managers**; **Loans** (`/admin/loans`: Active/All toggle over `GET /admin/loans?active=`, one card per loan on phones and a table from 900 px, a Forgive
  action per active loan — the API returns the player row, so the page closes the loan locally — and the
  20 banks read-only from `GET /admin/banks`: bank rules live in code); **Luxury** (`/admin/luxury` list reusing
  the catalogue list styles, and a routed editor at `/admin/luxury/new` and `/admin/luxury/:id` — `id` bound
  as a component input — with client-side checks that return the server's exact validation messages, so
  they translate through `dict/server.ts`, and a live photo preview). The luxury routes use the title key
  `admin.title.luxury` (in `dict/admin.ts`). A forgiven loan shows a "Forgiven" badge in the player's
  bank history (`LoanDto.forgiven`). Diamonds (§7b): Players shows each balance and badge count and a
  💎 action (give/take with a note, `POST /admin/players/{id}/diamonds`); Overview shows diamonds
  held / earned / spent, badges owned, boosts running, badges bought per badge, avatars bought and
  avatars in use / bought per avatar. Taxes (§5): Overview has an unpaid-taxes tile (total paid,
  companies owing); Players shows a player's unpaid taxes when there are any.

### GameService — the idle-game loop

`core/services/game.service.ts` is `providedIn: 'root'` and is the single source of game truth. It is
deliberately stateful and long-lived. All state signals are exposed read-only (`asReadonly()`); mutate through
the service's methods.

- **Bootstrap**: `bootstrap()` calls `GET /game/state` — the only endpoint that credits offline earnings.
  A `404` means "no company yet", not an error. `loaded` flips **only on a success or a 404** — that is what
  separates "still fetching" from "this player has none". Never set it on another failure: the dashboard
  would offer to create a company to a player who has one (seen after time away, when the sleeping Render
  API made Netlify answer 502). Each attempt times out after 15 s and transient failures (no answer,
  timeout, 502/503/504) are retried up to 3 times with a 3 / 6 / 9 s back-off (≈ 20–80 s in all) while
  `wakingServer` tells the spinner to say so;
  if it still fails, `LayoutComponent` resets the game and sends the player to `/login?reason=expired`,
  where a notice explains it.
- **Ticker**: `setInterval` every 50 ms adding `cashRate() × (real seconds since the previous tick)` to the
  local `cash` signal — **elapsed-time based, never a fixed `/20` step**: browsers throttle timers, a fixed step
  under-counted, and the server accepts a too-low figure (it only clamps high ones). `cashRate` is the
  server's `incomePerSecond` **verbatim** — the prestige multiplier is already baked into it, so never
  re-derive it from `passiveIncomePerSecond` plus the businesses — times `boostMultiplier` while an income
  boost runs (`now < boostUntil`, on the 1-second `now` clock); the server's rates never include the boost.
- **Sync**: `POST /game/sync` every 5 s. When the response says `adjusted`, the client **must** adopt
  `acceptedCash`; it does, and raises `syncAdjusted` so the UI can say so — an info toast that closes itself after 4 s.
- **Spending syncs first.** `Company.DeductCash` on the server compares the price against its *last recorded*
  cash and never accrues, so every money-moving call goes through `afterSync(...)`: `openBusiness`,
  `buyAsset`, `levelUpBusiness`, `automateBusiness`, `closeBusiness`, `prestige`, `payTaxes` / `payAllTaxes`,
  `takeLoan` / `repayLoan`, every `storeAction`, and `spend(price, purchase)` — the helper for purchases a
  feature service owns (luxury). Purchases then deduct the price from the local purse (`closeBusiness`
  deducts nothing; bank, tax and store calls adopt the server's cash instead). Skipping this makes the server reject
  purchases the player can visibly afford and silently loses `allTimeEarnings` (the leaderboard ranking).
- **Leaving the page**: `fetch(..., { keepalive: true })` to `/game/sync` on `visibilitychange`/`pagehide`.
  Not `sendBeacon` — it cannot attach the bearer token the endpoint requires.
- **Hidden tab = away.** On `hidden` the service flushes once and **stops the ticker and sync loops**; on
  `visible` it re-runs `bootstrap()` (`GET /game/state`), which credits the time away and raises the
  welcome-back dialog. Background syncs must not run: each one moves the server's `lastSyncAt` forward and
  erases the window `/state` would credit — a tab left open on a locked phone used to earn nothing.
  The dialog only shows for ≥ 60 s away (`MIN_AWAY_SECONDS`), so a reload does not pop it.
- **`TimeSpan` parsing**: `elapsed` gains a day prefix past 24 h (`"1.06:00:00"`). Use `timeSpanSeconds()` /
  `formatElapsed()` in `core/game/format.ts`; never split on `:` by hand.
- **Prestige** is a cash purchase behind a confirmation dialog in `DashboardComponent`
  (`confirmingPrestige`) showing cash before/after, the multiplier change and that businesses are kept.
- **Managers**: `automateBusiness(id, managerCost)` (sync first, deduct locally), surfaced as the "Hire a
  manager" card in `ManageBusinessComponent`, which calls `refreshCompany()` after hiring — `isAutomated` and the
  company's `offlineIncomePerSecond` are server-computed. A manager works a **4-hour shift** (`managerUntil`)
  and is then rehired: decide "on shift" with `shiftMsLeft(biz, game.now())` from `core/game/managers.ts`,
  never with `isAutomated` (true only as of the response). `GameService.now` is a 1-second clock signal
  driven by the ticker, for countdowns. `managerName` is picked by the server; `core/game/managers.ts`
  (`managerAvatar`) gives each name a stable emoji for the dialog and the dashboard badge.
- **Levels**: `levelUpBusiness(id, cost)` (sync first, deduct locally; `ManageBusinessComponent` then calls
  `refreshCompany()`). The server
  sends `level`, `levelMultiplier`, `nextLevelCost` and `nextLevelIncomePerSecond` (both `null` at max) —
  never re-derive the formula client-side; `netIncomePerSecond` already includes the level.
- **Achievements / profile** (`features/007-player-profile.md`): `/sync` responses carry `newAchievements`
  (each reported once); `GameService` queues them in `achievementToasts` (auto-dismissed after 6 s) and
  `LayoutComponent` renders them — toast styles live in `styles.scss` to keep the layout under its 4 kB style
  budget. `/profile` (`features/profile/`) reads `GET /api/profile` through `ProfileService`; progress
  formatting follows each achievement's `unit` (`money` | `count`), never the code name.
- **Bank** (contract §6d, `features/bank/`): `loadBank()`, `takeLoan(offerId)`, `repayLoan()`; state in the
  `bank` signal. Take/repay go through `afterSync(...)` like every spend, then **adopt the server's `cash`**
  from the `BankDto` and `refreshCompany()` (net worth subtracts the debt). `loadBank()` does *not* adopt
  cash — a plain GET's cash is stale, like `GET /game/company`. Installments are collected by the server on
  `/sync` and `/state`: a `/sync` with `loanPayment` adopts `acceptedCash` and raises `bankToast` (6 s;
  paid / penalty / repaid variants) **instead of** the generic "balance corrected" toast; on `/state` it is
  a line in the welcome-back dialog, or the same toast when the dialog does not show. Either way the bank
  data is reloaded if the page was opened. Bank names/icons are server content (not translated); toast
  styles (`.bank-toast`, `.offline-loan`) live in `styles.scss` for the layout's budget.

- **Diamonds and the store** (contract §6e, `features/store/`, `features/011-diamonds-and-store.md`):
  `diamonds` is server truth — adopted from the company, every `/sync` (`diamonds`) and every store
  answer, never simulated. `boosted` / `boostMsLeft` drive the top-bar chip and `cashRate`.
  `loadStore()` does not adopt cash (stale GET); `buyBoost`, `doubleOffline`, `exchangeDiamonds`
  and `buyBadge` go through `storeAction` = `afterSync` + adopt the answer's cash, diamonds and boost
  + `refreshCompany()`. `featureBadge` moves no money: no sync, cash not adopted. The welcome-back
  dialog offers "Double it" from `OfflineEarningsDto.doubleOffer`. Badge names are `badge.<id>` keys
  (`dict/store.ts`), like achievements; history reasons are `store.reason.<reason>`.
- **Avatars** (`features/012-avatars.md`): `game.avatar()` (from `CompanyDto.avatar`) feeds the top bar
  and the "More" sheet; the profile and the leaderboard use the `avatar` in their own payloads. Always
  render with `<app-avatar>` (it falls back to the initial). `buyAvatar` goes through `storeAction`;
  `selectAvatar` does not sync and patches the company's `avatar` locally. The store opens a tab from
  `?tab=` (the profile picture links to `/store?tab=avatars`). Names are `avatar.<id>` keys.
- **Business taxes** (contract §5 *Taxes*, `features/013-business-taxes.md`): the server bills each
  business 7 % of its earnings every 24 h and **never takes it** — the player pays. `game.taxesDue()`
  (from `CompanyDto.taxesDue`, patched by every `/sync`) drives the dashboard banner (**Pay all**), the
  red 🧾 top-bar chip and `canPrestige` (false while > 0; the button then reads "Pay your taxes
  first"). Each business card and the manage dialog show `taxDue` (Pay) or `taxAccruing` + the time to
  `taxPeriodEndsAt` (`taxMsLeft` in `core/game/taxes.ts`); the dialog hides "Close" while tax is due.
  `payTaxes(id)` / `payAllTaxes()` go through `afterSync` and adopt `cash` **and** `company` from
  `TaxPaymentDto`. A `/sync` or `/state` with `taxBilled > 0` raises `taxToast` (6 s); `/sync` then
  refreshes the company (`/state` already carries it). Keys in `dict/taxes.ts`; styles are global (`.tax-*` in `styles.scss`).

- **Places and breakdowns** (contract §5, `features/014-business-places-and-equipment.md`): units
  break on a schedule the server sends ahead (`workingFrom` / `breaksAt`). `core/game/units.ts`
  derives every "now" figure on the 1-second clock — `cashRate` uses `companyIncomeNow`, the cards
  use `businessIncomeNow` / `brokenUnits`; never display `netIncomePerSecond` or a unit's
  `status` raw, they are as of the response. The places panel (`features/dashboard/places/`) lives
  in the manage dialog; `openPlace`, `levelUpPlace`, `buyUnit`, `repairUnit`, `repairAll` go
  through `businessSpend` (sync first, deduct locally, patch the business, refresh the company).
  `/sync` or `/state` with `autoRepairs > 0` raises `repairToast` (or a welcome-back line).
  Faults are `fault.<id>` keys in `dict/places.ts` (`faultLabel()` falls back to the server name).

Because the service survives navigation, components **must not blindly re-bootstrap it**: call
`ensureLoaded()`, which reuses live state and only fetches when there is none.

### Auth

`AuthService` keeps the session in `localStorage` under `rl_token`, `rl_refresh`, `rl_user` (all reads and
writes are wrapped — `localStorage` throws in private-mode Safari). Other keys: `rl_lang` (language) and
`rl_store_tab` (the store's last tab). Logout also removes a `rl_offline_shown` key from `sessionStorage`
that nothing writes any more — a leftover. `isAuthenticated`/`currentUser`/`token`
are signals read back from storage at construction, so a refresh keeps the session. Both tokens rotate on
every refresh — store the whole response.

`authInterceptor` attaches the bearer token and, on a 401 for a non-`/auth/` URL, refreshes once and retries.
Concurrent 401s **queue** on that single refresh via a module-level `ReplaySubject` and are retried with the
new token; if the refresh fails, the queue is errored and the user is logged out (to `/login?reason=expired`).
The refresh runs on its **own subscription** with a 20 s timeout, not on the request that hit the 401: if that
request were cancelled mid-refresh, `refreshCycle` would stay set and every later request would queue behind
it forever — the app stuck on its spinner until a manual logout.

### Styling

Light theme, mobile-first. The font is **Plus Jakarta Sans**, loaded from `index.html` (not `@import`);
money uses tabular digits (`.num`, or any `.stat__value`) so the ticker does not jitter.

`src/styles.scss` defines the design system: CSS custom properties on `:root` (`--bg`, `--surface[-2|-3]`,
`--text[-2|-3]`, `--border[-strong]`, `--primary[-hover|-soft|-ring]`, semantic `--money`/`--success`/
`--danger`/`--violet` each with a `-soft` tint, `--radius-*`, `--shadow-*`, `--topbar-h`/`--tabbar-h`,
`--safe-bottom`), and global blocks: `.btn` (`--primary|--success|--outline|--soft|--danger|--full|--lg|--sm`),
`.icon-btn`, `.link-btn`, `.card`, `.stat` (`__label`/`__value--money|primary|success|danger`), `.badge--*`,
`.alert--error|--success`, `.form-group`/`.form-input`/`.form-select`, `.page`/`.page-header`, `.spinner`,
`.empty-state`, `.loading-screen`, and **`.modal-overlay` / `.modal`** (`--panel` + `__header`/`__body` for
list dialogs). Modals are a **bottom sheet below 640 px** and centred above. Those blocks are global precisely
because dialogs are raised from more than one component — component SCSS is scoped and would not reach them.
Component SCSS should consume these tokens rather than redeclaring colours.

**Game navigation** (`LayoutComponent`): the desktop top nav lists all 8 pages (`nav`, Bourse
included) — labels from 1280 px, icons with a tooltip between 900 and 1279 px. On phones (< 900 px)
there is **no tab bar**: a floating round button (`.fab`, bottom-end) opens `.fab-menu` — avatar,
💎, language, every page of `nav` as a tile, cash/rate and log out; it closes on any navigation
(`features/017-bourse.md`). `--tabbar-h` now only reserves room under the content and above toasts
for that button. The top bar keeps only the money (cash, rate, 💎 chip) on phones. Language and
account are `.desk-only` in the top bar. The layout's SCSS is at its 4 kB budget: shell styles go in
`styles.scss` (as the toasts and the floating menu do).
- **Bourse** (contract §6f, `features/market/`): `MarketService` (feature service) loads the market
  and history; buy/sell go through `GameService.syncThenAdoptCash` (sync first, adopt the answer's
  `cash`, refresh the company). Prices refresh every 30 s on the page; stock names are content.

Main breakpoints: 480 / 640 / 768 / 900 / 1024 / 1280 px (plus a few local ones: 420, 600, 860, 960, 1100,
and max-width 1199 / 1279 for the nav). A list with more than ~4 columns is **cards below 900 px** and a table
only above (Loans, Banks) — a table scrolling sideways inside a card is unusable on a phone. Players is a card
grid at every width (1 → 3 → 5 columns). `LayoutComponent` shows the top nav at ≥ 900 px and a
fixed bottom tab bar below it (content gets `padding-bottom` for it). Icons are inline SVG via
`shared/components/icon` (`<app-icon name="…" />`) — add a `@case` there for a new one. Login and register
share `features/auth/auth.scss`.

Production budgets cap a component stylesheet at 4 kB (warning) / 8 kB (error); the last production build
(2026-10-09, taxes) was warning-free.

### Languages (i18n) — English, French, Arabic

Switchable at runtime, no reload: `<app-lang-switcher />` sits in the game top bar, the admin top bar,
the auth pages and the profile page. The choice is saved in `localStorage` (`rl_lang`); the first visit
follows the browser language. Angular's compile-time `@angular/localize` is **not** used.

- `core/i18n/i18n.ts` — a module-level `currentLang` signal, `setLang()`, and `t(key, params)` (falls
  back to English, then to the key; `{name}` placeholders). Arabic sets `<html dir="rtl">`.
- `core/i18n/translate.pipe.ts` — `{{ 'key' | t }}` / `{{ 'key' | t: { n: 3 } }}`. **Impure on purpose**:
  `t()` reads the language signal, so OnPush views re-render on a switch. In TS, call `t()` (inside a
  `computed()` it re-evaluates by itself). A message already stored in a signal (an error, a notice)
  stays in the language it was produced in.
- `core/i18n/dict/<feature>.ts` — one `{ en, fr, ar }` file per feature; **every key in all three**.
  `core.ts` holds the shared ones (prestige names, sectors, durations, client errors, page titles,
  achievements by `ach.<code>.title|desc`). Route `title`s are keys, translated by
  `TranslatedTitleStrategy`.
- **Server text**: business-rule messages are English; `toErrorMessage()` looks them up in
  `dict/server.ts` (keyed by the exact contract wording; variable ones matched by regex in
  `api-error.ts`). A new server message needs a line there, or it shows in English. Its `fallback`
  argument is a key. Catalogue/luxury names and descriptions, usernames and company names are content
  and stay as sent.
- **RTL**: use logical CSS (`margin-inline-start`, `text-align: start`, `inset-inline-end`), never
  physical left/right unless symmetric; a left/right-pointing icon gets `.rtl-flip`. Arabic glyphs come
  from the Cairo font (fallback after Plus Jakarta Sans). Money stays `$1,234` in every language.

### Prestige levels

The backend enum — `TheHustle`, `SmallBusiness`, `Entrepreneur`, `BusinessMogul`, `Tycoon`, `Billionaire`,
`GlobalEmpire` — lives in **one place**: `core/game/prestige.ts` (`PRESTIGE_ORDER`, short labels,
colours, `nextPrestigeLabel`; names are the `prestige.<Level>` i18n keys in `core/i18n/dict/core.ts`). Adding or renaming a level means editing that file, those keys and the `PrestigeLevel`
union in `core/models/game.models.ts` — nothing else. Sector icons live in `core/game/sectors.ts`.

Thresholds are **not** mirrored on the client: use `company.nextPrestigeThreshold`, and detect the top level
with `prestigeLevel === 'GlobalEmpire'` rather than comparing against `decimal.MaxValue`.

## Conventions

- TypeScript is strict, with `noPropertyAccessFromIndexSignature`, `noImplicitReturns` and
  `strictTemplates` on. Prefer typed records over index-signature maps; `Record<PrestigeLevel, …>` reads
  cleanly with dot access.
- Components declare `standalone: true` explicitly, use `ChangeDetectionStrategy.OnPush`, and import only
  what the template uses (built-in `@if`/`@for` need no `CommonModule`).
- Dependencies come from `inject()`, not constructor parameters.
- Forms are template-driven (`FormsModule` + `ngModel`); `@angular/forms` reactive APIs are unused so far.
- Local UI state uses `signal()`; derived state uses `computed()`. Avoid `effect()` for state propagation.
- Format money in templates with the `money` / `rate` pipes, not by calling a service method.
- Surface backend failures through `toErrorMessage()` — the API has no ProblemDetails envelope, and 401/429
  and framework-level 400s have empty bodies that `err.error` cannot describe.
- Comments in the codebase are a mix of French and English — match the surrounding file.

### Luxury photos

`public/luxury/<id>.jpg` are Wikimedia Commons photos (CC0 / public domain / CC BY / CC BY-SA) referenced
by `imageUrl` in `luxury_catalogue`. **Always render `imageCredit` with the photo** (global `.photo-credit`
class) — the licenses require attribution. Replacing a photo means replacing its credit and source URL in the
database too (`features/008-luxury-collection.md`).

Photo frames are a fixed aspect ratio with the `img` absolutely positioned and `object-fit: cover` — a
portrait photo (paintings) would otherwise stretch its card. Categories (16) come from the server as enum
names; display them through `LUXURY_CATEGORY_LABELS` / `LUXURY_CATEGORY_ICONS` in `luxury.models.ts`, and add
both entries when the backend appends a category.
