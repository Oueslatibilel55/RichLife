# RichLife — API contract

**Source of truth** for every HTTP exchange between `RichLife` (.NET backend) and
`idle-startup-frontend` (Angular). Any API change updates this file **first**, then the
backend, then the frontend.

- Base URL (dev): `http://localhost:5187` — **fixed**, under Aspire and standalone alike. Pinned in `RichLife/src/RichLife.AppHost/Program.cs` (`IsProxied = false`); see `RichLife/CLAUDE.md` → *Aspire dev environment*.
- Deployed: the API is `https://richlife.onrender.com`, but clients reach it **same-origin
  through the Netlify site's `/api/*` proxy** (`netlify.toml`), so the frontend still calls
  `/api`.
- Every route below is already prefixed with `/api`. The Angular `environment.apiUrl` is
  `/api` (same-origin; in dev `ng serve` proxies it to `http://localhost:5187`), so frontend
  calls drop the `/api` segment.
- CORS: only `http://localhost:4200` (policy `angular`), any header, any method.
- OpenAPI/Scalar: `/openapi/v1.json` and `/scalar` — **development only**.

---

## 1. Conventions

| Concern | Rule |
|---|---|
| JSON casing | **camelCase** on the wire, both directions (ASP.NET `JsonSerializerDefaults.Web`). Backend records are PascalCase; the serializer converts. |
| Enums | **Strings**, never numbers (`JsonStringEnumConverter` is registered globally): `prestigeLevel: "SmallBusiness"`, `sector: "Transport"`. |
| Dates | `DateTime` in UTC, ISO-8601 with `Z` (`"2026-10-01T09:14:22.431Z"`). The backend is UTC throughout; never send a local time. |
| Durations | `TimeSpan` serializes as `"hh:mm:ss(.fffffff)"` — e.g. `"04:00:00"`. Only `OfflineEarningsDto.elapsed` uses it. |
| Money / rates | JSON numbers, backed by `decimal` server-side. Do not round-trip through a float where precision matters. |
| Ids | `Guid` as a lowercase dashed string. Catalogue ids are **slug strings** (`"food-cart"`, `"menu-item"`), not Guids. |
| Auth | `Authorization: Bearer <accessToken>` on everything except `/api/auth/*` and `/api/leaderboard`. Access token lives **1 hour**; refresh token **7 days**, rotated on every use. `/api/admin/*` additionally needs the `Admin` role claim (see §7). **Admins are staff, not players**: an admin token is refused (`403`) on `/api/game/*` (§4, §5). |
| Pagination | None. The only list that limits is `/api/leaderboard` via `?take=` (default 50, clamped to 1..100). Lists are **bare JSON arrays**, never wrapped in an envelope. |
| Content type | `application/json` for every request with a body, except `/api/game/sync-beacon` (see its note). |

### Error format

There is **no ProblemDetails / RFC 9457 envelope**. Expected business failures return a
bare JSON string:

```http
HTTP/1.1 400 Bad Request
Content-Type: application/json

"Insufficient funds."
```

Angular's `HttpClient` parses that into a plain `string`, so `err.error` *is* the message.

| Status | Body | When |
|---|---|---|
| `400` | JSON string | A business rule rejected the command (`Result.Fail`). |
| `401` | *empty* | Missing / expired / invalid bearer token, bad credentials, or a token whose `nameid` claim is absent or not a Guid. |
| `403` | *empty* | Valid token without the `Admin` role on `/api/admin/*`, or **with** it on `/api/game/*` (admins do not play). |
| `404` | JSON string | `GET /api/game/state` and `GET /api/game/company` when the player has no company; `GET /api/admin/catalogue/{id}` for an unknown id. |
| `429` | *empty* | Rate limit exceeded. |
| `400` | *empty* | Framework-level binding failure (malformed JSON body, required query parameter missing). Not the `Result` path — there is no message to display. |
| `500` | dev page / empty | Unhandled. There is no global exception handler. See *Known gaps*. |

### Rate limits

Sliding window, applied per server (not per user).

| Policy | Applies to | Limit |
|---|---|---|
| `auth` | `/api/auth/*` | 10 requests / 60 s |
| `game-actions` | `/api/game/*`, `/api/game/businesses/*`, `/api/leaderboard`, `/api/admin/*` | 20 requests / 10 s |

### Known gaps (documented, not endorsed)

- **No input validation on `/api/auth/register`.** A blank `username` or `email` reaches
  `Player.Create`, which throws `ArgumentException` → **500**, not 400. The client must
  validate `username` (≤ 30 chars), `email` (≤ 254) and `country` (exactly 2 chars)
  itself. There is no server-side password policy.
- `nextPrestigeThreshold` is `decimal.MaxValue` (`7.922816251426434e+28`) at
  `GlobalEmpire`. Prefer testing `prestigeLevel === "GlobalEmpire"` over comparing against
  that number.
- `GET /api/leaderboard` is registered with a trailing slash — the exact path is
  **`/api/leaderboard/`**.

---

## 2. Shared schemas

### `CompanyDto`

```json
{
  "id": "6f1f4b4e-0d28-4f0b-9f1c-2b0e6f1a77d3",
  "name": "Oueslati Holdings",
  "cash": 18420.5,
  "passiveIncomePerSecond": 3.54,
  "incomePerSecond": 37.42,
  "offlineIncomePerSecond": 4.1772,
  "netWorth": 76420.5,
  "allTimeEarnings": 128430.75,
  "prestigeLevel": "SmallBusiness",
  "prestigeCount": 1,
  "prestigeMultiplier": 1.18,
  "nextPrestigeThreshold": 200000,
  "lastSyncAt": "2026-10-01T09:14:22.431Z",
  "diamonds": 85,
  "boostUntil": null,
  "boostMultiplier": 2,
  "businesses": []
}
```

| Field | Meaning |
|---|---|
| `diamonds` | The premium currency balance (§6e). A whole number. |
| `boostUntil` | End of the running **income boost** (§6e), `null` if none was ever bought. While `now < boostUntil` every income — online and offline — is × `boostMultiplier`. **`incomePerSecond` and `offlineIncomePerSecond` never include the boost**: the client multiplies its ticker rate itself and drops back when `boostUntil` passes. |
| `incomePerSecond` | **Online** rate: `(passiveIncomePerSecond + Σ business.netIncomePerSecond) × prestigeMultiplier`. This is the number the client ticker must simulate — the multiplier is **already applied**. |
| `offlineIncomePerSecond` | Same formula, but counting only businesses whose manager shift is running at response time (`isAutomated: true`). The rate actually paid while away also depends on when each shift ends — see `GET /api/game/state`. |
| `netWorth` | `cash` + company assets + Σ `business.totalValue` + Σ luxury items bought (at their price, §6c) **−** what is still owed on an active bank loan (`outstanding`, §6d). |
| `allTimeEarnings` | Every dollar income has ever produced, cumulative. Never decreases; **survives prestige**; refunds from closing a business do not count. This is what ranks `/api/leaderboard`. Shown on the dashboard — see `features/001-all-time-earnings-on-company.md`. |
| `prestigeMultiplier` | `1 + 0.18 × prestigeCount`. |
| `nextPrestigeThreshold` | **Cash price** of the **next** prestige (see §4 `/prestige`). Compare against `cash`, not `netWorth` — business value cannot pay for it. |
| `lastSyncAt` | Server watermark. Offline accrual and the `/sync` ceiling are both measured from it. |

### `BusinessDto`

```json
{
  "id": "c3a0e2d1-9b77-4a1e-8f3a-5d2c9e7b1a04",
  "catalogueId": "taxi-fleet",
  "name": "Taxi Fleet",
  "sector": "Transport",
  "requiredPrestige": "SmallBusiness",
  "openingCost": 50000,
  "netIncomePerSecond": 28.148148,
  "totalValue": 58000,
  "isAutomated": false,
  "isForSale": false,
  "askingPrice": null,
  "assetCount": 1,
  "managerCost": 100000,
  "managerName": null,
  "managerUntil": null,
  "level": 3,
  "levelMultiplier": 1.2,
  "nextLevelCost": 78125,
  "nextLevelIncomePerSecond": 30.493981
}
```

`catalogueId` is the join key back to the catalogue — **match on it, never on `name`**.
`netIncomePerSecond` is gross income (base + assets) **× `levelMultiplier`**, minus the
amortized salary (`monthlySalaryCost / 2592000`), **before** the prestige multiplier.
`level` starts at 1 (max 100). `levelMultiplier` = `(1 + 0.10 × (level − 1)) × 2^milestones`,
where each of levels **10, 25 and 50** reached doubles it. `nextLevelCost` is the price of
the next level-up (`openingCost × 1.25^(level − 1)`) and `nextLevelIncomePerSecond` what
`netIncomePerSecond` becomes after it — both `null` at max level. See
`/level-up` in §5. `totalValue` is the
liquidation value (opening cost + assets) — what a close refunds, minus the fee.
A manager works a **4-hour shift** that starts when hired (see `/automate` in §5).
`managerUntil` is when the current or last shift ends (UTC), `null` if never hired.
`isAutomated: true` means a shift is running **at response time** (`managerUntil > now`) —
it goes stale as time passes, so a client showing a countdown compares `managerUntil` with
its own clock. `managerCost` is the price of a shift — `2 × openingCost` — paid again for
every rehire. `managerName` is the current or last manager's first name (`"Lucy"`), `null`
until the first hire — picked at random server-side from the `manager_names` table on every
hire. A company's running shifts never share a name while unused names remain.

### `BusinessCatalogueDto`

```json
{
  "id": "food-cart",
  "name": "Food Cart",
  "sector": "Hospitality",
  "requiredPrestige": "TheHustle",
  "openingCost": 500,
  "baseIncomePerSecond": 2,
  "monthlySalaryCost": 0,
  "baseEmployeeCount": 0,
  "description": "A simple street food cart. Low cost, instant income.",
  "canAfford": true,
  "isOwned": false,
  "availableAssets": [
    { "id": "menu-item",   "name": "Menu item",    "price": 200, "incomePerSecond": 0.5, "unlockAtAssetCount": 0, "isUnlocked": true  },
    { "id": "menu-item-2", "name": "Premium dish", "price": 500, "incomePerSecond": 1.5, "unlockAtAssetCount": 5, "isUnlocked": false }
  ]
}
```

`canAfford`, `isOwned` and per-asset `isUnlocked` are **computed server-side against the
caller's company**. The client must not recompute them. `unlockAtAssetCount` (added
2026-10-08) is the number of assets — of any type — the business must own before this one
can be bought; it is for display ("2 more to unlock", against `BusinessDto.assetCount`).
`isUnlocked` stays the authority.

### `AuthResponse`

```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIs...",
  "refreshToken": "n9Qk3...==",
  "playerId": "0f6b2c58-1a9d-4d7e-b2d6-9a1f8c5e4b07",
  "username": "bilel"
}
```

### Enum values

- `prestigeLevel` / `requiredPrestige`: `TheHustle`, `SmallBusiness`, `Entrepreneur`,
  `BusinessMogul`, `Tycoon`, `Billionaire`, `GlobalEmpire`.
- `sector`: `Transport`, `RealEstate`, `StockMarket`, `TechStartup`, `Hospitality`,
  `Energy`, `Services`.

---

## 3. Auth — `/api/auth`

Anonymous. Rate limit `auth` (10/min).

### `POST /api/auth/register`

```json
{ "username": "bilel", "email": "bilel@example.com", "password": "s3cret!", "country": "TN" }
```

→ `200` `AuthResponse` · `400` `"Username already taken."` | `"Email already registered."`
· `429` · `500` on a blank username/email (see *Known gaps*).

`email` is lower-cased server-side and must be unique. `country` is an ISO-3166 alpha-2
code (exactly 2 characters).

### `POST /api/auth/login`

```json
{ "email": "bilel@example.com", "password": "s3cret!" }
```

→ `200` `AuthResponse` · `401` *(empty body — the server deliberately does not
distinguish unknown email from wrong password)* · `429`.

### `POST /api/auth/refresh`

```json
{ "refreshToken": "n9Qk3...==" }
```

→ `200` `AuthResponse` · `401` if unknown or expired · `429`.

**Both** tokens in the response are new: the refresh token rotates and the old one stops
working. Store both.

---

## 4. Game — `/api/game`

**Bearer required** on every endpoint in this section, from a **player** — an admin token gets `403`. Rate limit `game-actions`.

### `POST /api/game/company` — create or fetch

```json
{ "companyName": "Oueslati Holdings" }
```

→ `200` `CompanyDto` · `400` JSON string · `401`.

Idempotent: if the player already has a company it is returned unchanged and
`companyName` is ignored. `name` is capped at 50 characters by the database.

### `GET /api/game/company`

→ `200` `CompanyDto` · `404` `"Company not found."` · `401`.

Read-only snapshot. **Does not accrue offline income and does not move `lastSyncAt`.**

### `GET /api/game/state` — load **and** credit offline earnings

→ `200` `OfflineEarningsDto` · `404` `"Company not found."` · `401`.

```json
{
  "elapsed": "06:12:40",
  "earned": 60508.8,
  "cashBefore": 18420.5,
  "cashAfter": 78929.3,
  "capped": true,
  "loanPayment": null,
  "doubleOffer": { "amount": 60508.8, "price": 15, "until": "2026-10-01T09:44:22Z" },
  "company": { "id": "6f1f4b4e-...", "cash": 78929.3, "businesses": [] }
}
```

The only endpoint that credits time spent away. The paid window is `lastSyncAt` →
`lastSyncAt + min(elapsed, 4 h)` (`capped: true` means `elapsed` exceeded the cap). Within
it, **base income** is paid for the whole window, and **each business** only for the part
of the window its manager's shift covers (`lastSyncAt` → `min(window end, managerUntil)`),
all × `prestigeMultiplier` (and × `boostMultiplier` for the part of the window an income boost covers, §6e). Example: hire at 11:00 (shift until 15:00), leave at 12:00, back
at 18:00 → window 12:00–16:00: base 4 h, that business 3 h.
It advances `lastSyncAt` to now, so it cannot double-credit with `/sync`.
After crediting, it collects every **bank installment** that fell due while away (§6d);
`loanPayment` sums them (`null` when none was due) and `cashAfter` is net of them.
`doubleOffer` (`null` when nothing was earned) lets the player pay `price` diamonds to receive
`amount` again, until `until` — see `POST /api/game/store/double-offline` (§6e). A later
`/state` that earns something replaces the offer.
**This is the correct bootstrap call on app start**, not `GET /api/game/company` — and on
**every return to a visible tab**: the client stops its `/sync` loop while hidden (a
background sync would move `lastSyncAt` and erase this window) and calls `/state` when it
comes back. `elapsed` is a .NET `TimeSpan`: past 24 hours it gains a day prefix with a
**dot** (`"1.06:00:00"` = 30 h).

### `POST /api/game/sync`

```json
{ "cash": 18420 }
```

→ `200` `SyncResultDto` · `400` `"Cash cannot be negative."` · `401`.

```json
{
  "acceptedCash": 18420, "adjusted": false,
  "newAchievements": [ { "code": "first-business", "title": "Open for business", "icon": "🏪", "diamonds": 10 } ],
  "loanPayment": null,
  "diamonds": 95
}
```

The client simulates cash locally and reports it. The server clamps it to
`lastRecordedCash + incomePerSecond × elapsedSeconds × 1.05`. `adjusted: true` means the
figure was clamped — the client **must** adopt `acceptedCash` as its new truth.
`lastSyncAt` moves to now on every call.

After clamping, every sync collects the **bank installments** now due (§6d). When one is
collected, `loanPayment` describes it and `acceptedCash` is net of it — so `adjusted` is
`true` and the client adopts the figure as usual (it should announce the payment rather
than a generic "balance corrected").

Every sync also checks achievements (§6b): `newAchievements` lists the ones unlocked by this
call — usually `[]` — so the client can announce them. Each is reported exactly once, with the
diamonds it paid (§6e). `diamonds` is the balance after the call.

The sync ceiling includes a running income boost (§6e): seconds of the window before
`boostUntil` count `boostMultiplier` times.

### `POST /api/game/sync-beacon`

Same body as `/sync`, intended for `navigator.sendBeacon` on tab close.

→ `200` *(empty body)* · `400` `"Malformed sync payload."` | `"Empty sync payload."` ·
`401`.

The body is deserialized manually (case-insensitive property names), so **any content
type is accepted**. The endpoint still sits behind `RequireAuthorization()` and therefore
still needs a bearer token — which `sendBeacon` cannot attach. See mismatch **M1**.

### `POST /api/game/prestige`

No request body.

→ `200` `CompanyDto` (after the purchase) · `401` ·
`400` `"Company not found."` | `"Already at maximum prestige."` |
`"Prestige costs $200,000 in cash."`

Prestige is a **purchase**, not a reset (changed 2026-10-08 — see
`features/003-prestige-is-a-purchase.md`). Offline earnings are credited
first, then **cash** is measured against `nextPrestigeThreshold` — the price. On success
the price is deducted from `cash`; the remaining cash and **every business and asset are
kept**. `prestigeLevel` and `prestigeCount` are incremented (so `prestigeMultiplier` rises)
and `passiveIncomePerSecond` → `3 × newPrestigeMultiplier`. Spending the price does not
reduce `allTimeEarnings`. Each prestige also gives **diamonds** (§6e). Like any spend, the client syncs first, then adopts the returned
`CompanyDto` as its new truth (cash and rate).

| From → to | Price (`nextPrestigeThreshold`) |
|---|---|
| `TheHustle` → `SmallBusiness` | 25,000 |
| `SmallBusiness` → `Entrepreneur` | 200,000 |
| `Entrepreneur` → `BusinessMogul` | 2,000,000 |
| `BusinessMogul` → `Tycoon` | 20,000,000 |
| `Tycoon` → `Billionaire` | 250,000,000 |
| `Billionaire` → `GlobalEmpire` | 3,000,000,000 |

---

## 5. Businesses — `/api/game/businesses`

**Bearer required**, from a **player** (admin token → `403`). Rate limit `game-actions`.

### `GET /api/game/businesses/catalogue`

→ `200` `BusinessCatalogueDto[]` · `400` `"Company not found."` · `401`.

Returns only **active** entries at or below the caller's current prestige level, each
already flagged with `canAfford` / `isOwned` and per-asset `isUnlocked`. Ordered by
`requiredPrestige`, then the admin-set display order; assets likewise.

The catalogue is **content stored in the database** and editable through §7, so the client
must never hardcode a business, an asset or a price. It may change between two calls (the
server caches it for at most 10 minutes, and evicts on every admin edit). An edit applies
to businesses opened **after** it: an owned business keeps the cost and income it was
opened with, and a bought asset keeps its price and income. A retired entry disappears
from this list, but players who own it keep it — and can still buy its assets via the
route below.

### `POST /api/game/businesses/{catalogueId}`

`catalogueId` is the catalogue slug (`"food-cart"`). **No request body.**

→ `200` `BusinessDto` (the newly opened business) · `401` · `400` with one of:
`"Company not found."`, `"Business not found in catalogue."`,
`"Requires prestige {Level}."`, `"You already own this business."`,
`"Insufficient funds."`.

One business per catalogue entry per company, enforced by the aggregate *and* by a unique
index on `(CompanyId, CatalogueId)`. A **retired** entry answers
`"Business not found in catalogue."`, exactly like an unknown one.

### `POST /api/game/businesses/{businessId}/assets/{assetCatalogueId}`

`businessId` is a Guid; `assetCatalogueId` is the asset slug (`"menu-item"`).
**No request body.**

→ `200` `BusinessDto` (the business, with updated `assetCount`, `netIncomePerSecond` and
`totalValue`) · `401` · `400` with one of:
`"Company not found."`, `"Business not found."`, `"Business not found in catalogue."`,
`"Asset not available for this business."`, `"Need {n} assets to unlock this."`,
`"Insufficient funds."`.

An asset belongs to a business *type* — only assets listed in that business's
`availableAssets` can be bought for it.

### `POST /api/game/businesses/{businessId}/automate` — hire a manager

`businessId` is a Guid. **No request body.** Added 2026-10-08 — see
`features/004-business-managers.md`.

→ `200` `BusinessDto` (`isAutomated: true`, a new `managerName`, `managerUntil` = now + 4 h)
· `401` · `400` with one of: `"Company not found."`, `"Business not found."`,
`"Business already has a manager."` (a shift is still running), `"Insufficient funds."`.

Starts a **4-hour shift** for `managerCost` (`2 × openingCost`) in cash. The shift runs on
the clock whether the player is online or not; while online every business earns anyway,
so the shift only pays during time away (see `GET /api/game/state`). When it ends the
manager leaves and the same call **rehires** — same price, a new random name. Like every
spend, the client syncs first; spending does not reduce `allTimeEarnings`. Closing the
business does not refund the shift; prestige keeps a running one.

### `POST /api/game/businesses/{businessId}/level-up`

`businessId` is a Guid. **No request body.** Added 2026-10-08 — see
`features/005-business-levels.md`.

→ `200` `BusinessDto` (level + 1, new `netIncomePerSecond`) · `401` · `400` with one of:
`"Company not found."`, `"Business not found."`, `"Business is already at max level."`,
`"Insufficient funds."`.

Costs `nextLevelCost` in cash. The higher income applies immediately online, and offline
too if the business has a manager. Like every spend, the client syncs first; spending does
not reduce `allTimeEarnings`. Level spend is **not** part of `totalValue` (not refunded on
close); prestige keeps the level.

### `DELETE /api/game/businesses/{businessId}?emergency={bool}`

→ `200` *(empty body)* · `401` · `400` `"Company not found."` | `"Business not found."`.

`emergency` is a **required** query parameter — omitting it is a framework `400` with an
empty body. The refund is `totalValue × (1 − fee)`: fee is **10 %** when
`emergency=false`, **25 %** when `emergency=true`. A refund returns capital, so it does
**not** count toward `allTimeEarnings`.

---

## 6. Leaderboard — `/api/leaderboard/`

Anonymous. Rate limit `game-actions`. Note the **trailing slash**.

### `GET /api/leaderboard/?take=50`

`take` is optional, defaults to 50, clamped server-side to 1..100.

→ `200`:

```json
[
  {
    "rank": 1,
    "username": "bilel",
    "country": "TN",
    "companyName": "Oueslati Holdings",
    "allTimeEarnings": 1284300.75,
    "prestigeLevel": "Entrepreneur",
    "prestigeCount": 2,
    "badgeIcon": "🦄"
  }
]
```

Ranked by **all-time earnings**, not net worth. `badgeIcon` is the player's featured badge (§6e),
`null` if none. `rank` is 1-based and computed over the
returned page. Players without a company, and admins, are excluded.

---

## 6b. Profile — `/api/profile`

Added 2026-10-08 — see `features/007-player-profile.md`. **Bearer required, from a player**
(admin token → `403`). Rate limit `game-actions`.

### `GET /api/profile` → `200` `ProfileDto` · `401` · `403`

```json
{
  "username": "bilel", "email": "bilel@richlife.dev", "country": "TN",
  "memberSince": "2026-10-01T09:00:00Z",
  "rank": 2, "rankedPlayers": 12,
  "company": {
    "name": "Oueslati", "createdAt": "2026-10-01T09:05:00Z",
    "prestigeLevel": "SmallBusiness", "prestigeCount": 1, "prestigeMultiplier": 1.18,
    "cash": 18420.5, "netWorth": 76420.5, "allTimeEarnings": 128430.75, "incomePerSecond": 37.42,
    "businesses": 4, "assets": 11, "managersOnShift": 2, "managersHired": 3,
    "highestBusinessLevel": 7, "diamonds": 85
  },
  "badges": [ { "id": "unicorn", "icon": "🦄", "name": "Unicorn", "rarity": "epic", "purchasedAt": "…" } ],
  "featuredBadgeId": "unicorn",
  "luxury": [ { "id": "rolex-submariner", "name": "Rolex Submariner", "category": "Watch", "price": 150000,
               "imageUrl": "/luxury/rolex-submariner.jpg", "imageCredit": "…", "purchasedAt": "…" } ],
  "achievementsUnlocked": 6, "achievementsTotal": 18,
  "achievements": [
    {
      "code": "earn-1m", "title": "Millionaire", "description": "Earn $1,000,000 all-time.",
      "icon": "🤑", "unlocked": false, "unlockedAt": null,
      "current": 128430.75, "target": 1000000, "unit": "money"
    }
  ]
}
```

- `company` is `null` (and `rank` `null`) until the player creates one.
- `rank` is the position on the all-time-earnings leaderboard among `rankedPlayers` (players
  with a company; admins excluded).
- `badges` are the badges bought in the store (§6e), newest first; `featuredBadgeId` the one shown
  next to the name (also on the leaderboard), `null` if none.
- `managersHired` = businesses that have had a manager at least once; `managersOnShift` =
  shifts running now.
- **Achievements** are defined in code (rules, like `GameConstants`), in display order.
  `current` / `target` give the progress (`current` is capped at `target` once unlocked);
  `unit` is `"money"` (format as dollars) or `"count"`.
  An achievement is **saved when first met** — checked on every `/sync` and on this call —
  so it stays unlocked even if the metric later drops (e.g. a business is closed).
  `unlockedAt` is when the server first saw it met. An admin reset clears them.

---

## 6c. Luxury — `/api/game/luxury`

Added 2026-10-08 — see `features/008-luxury-collection.md`. **Bearer required, from a
player** (admin → `403`). Rate limit `game-actions`.

Luxury items are status: they earn nothing, but each one bought is shown on the profile and
counts toward `netWorth` at its price. One of each item per company. The list is content in
the database (`luxury_catalogue`), seeded with 76 items in 16 categories from P2 to P7.

### `GET /api/game/luxury` → `200` `LuxuryItemDto[]` · `400` `"Company not found."` · `401` · `403`

Every active item (locked ones included, so players see what they are working toward),
ordered by required prestige then price.

```json
{
  "id": "lamborghini-aventador", "name": "Lamborghini Aventador SVJ", "category": "Car",
  "description": "A V12 that sounds like money leaving.", "price": 25000000,
  "requiredPrestige": "BusinessMogul",
  "imageUrl": "/luxury/lamborghini-aventador.jpg",
  "imageCredit": "Calreyn88 · CC BY-SA 4.0",
  "imageSourceUrl": "https://commons.wikimedia.org/wiki/File:…",
  "isUnlocked": false, "isOwned": false, "canAfford": false
}
```

- `category`: `Watch`, `Motorbike`, `Car`, `Property`, `Island`, `Yacht`, `Aircraft`, `Jewelry`,
  `Fashion`, `Wine`, `Instrument`, `Art`, `Horse`, `SportsTeam`, `Experience`, `Collectible`.
- `imageUrl` is a path on the **frontend** origin (static file); `imageCredit` +
  `imageSourceUrl` must be shown with the photo (the licenses require attribution).
- `isUnlocked` = the company's prestige reaches `requiredPrestige`; `canAfford` compares
  against the server's last recorded cash (a hint, like the business catalogue).

### `POST /api/game/luxury/{id}` — buy

No body → `200` `OwnedLuxuryDto` · `401` · `403` · `400` with one of: `"Company not found."`,
`"Item not found."`, `"Requires prestige {Level}."`, `"You already own this."`,
`"Insufficient funds."`. Paid in cash (sync first, like every spend); does not reduce
`allTimeEarnings`. Kept through prestige; an admin reset clears the collection.

```json
{
  "id": "lamborghini-aventador", "name": "Lamborghini Aventador SVJ", "category": "Car",
  "price": 25000000, "imageUrl": "/luxury/lamborghini-aventador.jpg",
  "imageCredit": "Calreyn88 · CC BY-SA 4.0", "purchasedAt": "2026-10-08T14:00:00Z"
}
```

`GET /api/profile` (§6b) lists the same objects under `luxury` (newest first).

## 6d. Bank — `/api/game/bank`

Added 2026-10-09 — see `features/009-bank-loans.md`. **Bearer required, from a player**
(admin → `403`). Rate limit `game-actions`.

A player borrows from one of **20 banks** (fixed list, defined in code). Each prestige level
sees **5 offers**, the same for every player at that level; they are **regenerated every
6 hours** (at 00:00, 06:00, 12:00 and 18:00 UTC) — `offersRefreshAt` says when. Amounts
scale with prestige, like businesses:

| Prestige | Loan amounts |
|---|---|
| `TheHustle` | 5,000 – 40,000 |
| `SmallBusiness` | 30,000 – 250,000 |
| `Entrepreneur` | 200,000 – 2,000,000 |
| `BusinessMogul` | 2,000,000 – 20,000,000 |
| `Tycoon` | 20,000,000 – 200,000,000 |
| `Billionaire` | 200,000,000 – 2,500,000,000 |
| `GlobalEmpire` | 2,000,000,000 – 25,000,000,000 |

Rules:

- **One loan at a time.** A new one can be taken once the current one is fully repaid.
- Taking a loan adds `amount` to cash. It is borrowed, not earned: `allTimeEarnings` is
  untouched, and `netWorth` subtracts what is still owed.
- The loan costs `totalRepay = amount × (1 + interestRate)`, paid in `installments` equal
  payments of `installmentAmount`, **one every 6 hours** from the moment it is taken. The
  bank collects them itself, on the next `/sync` or `/state` after each falls due
  (several at once after time away).
- **Cash never goes negative.** If cash cannot cover an installment, the bank takes what is
  there (cash → 0), the unpaid part stays owed, and a **penalty of 10 %** of that unpaid part
  is added to the debt (`missedPayments` + 1). Installments then continue every 6 hours
  until `outstanding` reaches 0.
- **Repay all** pays `outstanding` at once from cash (no early-repayment discount).
- The loan survives prestige; an admin reset deletes the company's loans.
- Like every spend, the client syncs first. The take and repay responses carry the
  server's `cash`, which the client adopts. The `cash` in `GET /api/game/bank` is the last
  recorded figure (stale against the client ticker, like `GET /api/game/company`) — display
  only, never adopted.

### `GET /api/game/bank` → `200` `BankDto` · `400` `"Company not found."` · `401` · `403`

```json
{
  "cash": 18420.5,
  "offersRefreshAt": "2026-10-09T12:00:00Z",
  "paymentIntervalHours": 6,
  "penaltyRate": 0.10,
  "offers": [
    {
      "id": "81974-1-0", "bankId": "carthage-credit", "bankName": "Carthage Credit", "bankIcon": "🏺",
      "amount": 100000, "interestRate": 0.05, "totalRepay": 105000,
      "installments": 8, "installmentAmount": 13125
    }
  ],
  "activeLoan": null,
  "history": []
}
```

`offers` always has 5 entries, for the caller's **current** prestige level. An offer `id` is
only valid until `offersRefreshAt`.

`LoanDto` (`activeLoan`, and each `history` entry — the last 10 repaid loans, newest first):

```json
{
  "id": "5b2e…", "bankId": "carthage-credit", "bankName": "Carthage Credit", "bankIcon": "🏺",
  "principal": 100000, "interestRate": 0.05, "totalRepay": 105000,
  "installments": 8, "installmentAmount": 13125,
  "paid": 26250, "penalties": 0, "outstanding": 78750, "missedPayments": 0,
  "takenAt": "2026-10-09T08:00:00Z", "nextPaymentAt": "2026-10-09T20:00:00Z",
  "repaidAt": null, "forgiven": false
}
```

`outstanding = totalRepay + penalties − paid`. `nextPaymentAt` is `null` and `repaidAt` set
once closed — repaid, or **forgiven** by an admin (`forgiven: true`, §7b; `outstanding` is
then 0 and `paid` is what was actually paid).

### `POST /api/game/bank/loans/{offerId}` — take a loan

No body → `200` `BankDto` (with `activeLoan` set and `cash` including the amount) · `401` ·
`403` · `400` with one of: `"Company not found."`, `"You already have a loan. Repay it first."`,
`"This offer has expired."` (unknown id, or the offers rotated — reload them).

### `POST /api/game/bank/repay` — repay everything

No body → `200` `BankDto` (`activeLoan: null`, the loan now first in `history`) · `401` ·
`403` · `400` with one of: `"Company not found."`, `"You have no loan to repay."`,
`"Insufficient funds."`.

### `LoanPaymentDto` — on `/sync` and `/state`

```json
{ "bankName": "Carthage Credit", "paid": 13125, "penalty": 0, "outstanding": 65625, "repaid": false }
```

The total collected by that call (it may cover several installments), the penalty added for
any shortfall, what is still owed afterwards, and whether the loan is now fully repaid.

## 6e. Store and diamonds — `/api/game/store`

Added 2026-10-09 — see `features/011-diamonds-and-store.md`. **Bearer required, from a player**
(admin → `403`). Rate limit `game-actions`.

**Diamonds** (💎) are a second, whole-number currency. The server is the only source of truth:
the client never simulates them, and every gain and spend is a row in a ledger.

| Earned from | Diamonds |
|---|---|
| Creating a company (welcome gift) | 25 |
| Each achievement unlocked (§6b) | 10 |
| Each prestige (§4) | 20 × the new level's number (P2 → 40 … P7 → 140) |
| An admin (§7b) | any |

Ads will be a source later; diamonds cannot be bought, and **cash cannot be turned into
diamonds**. Everything priced in diamonds below is a rule in code (`GameConstants`, `BadgeCatalog`).

| Spend | Price | Effect |
|---|---|---|
| Income boost 1 h / 3 h / 8 h | 25 / 60 / 140 | Every income × 2 until `boostUntil` — online and offline. Buying while one runs **adds time** (never × 4); at most 24 h ahead. Counts toward `allTimeEarnings` like any income. |
| Double offline earnings | 15 | Pays the last `/state` earnings again (`doubleOffer`), once, within 30 minutes. Counts toward `allTimeEarnings`. |
| Exchange | n | Adds `n × diamondValue` cash. **Not** earnings (`allTimeEarnings` untouched). |
| Badge | 20–250 | Owned for good; shown on the profile; one can be featured next to the name. |

`diamondValue` (cash per diamond) follows prestige: 250 · 2,000 · 20,000 · 200,000 · 2,500,000 ·
30,000,000 · 300,000,000 (P1 → P7).

Like every spend, the client **syncs first**; each POST answers a fresh `StoreDto` whose `cash`,
`diamonds` and `boostUntil` the client adopts. The `cash` in `GET /api/game/store` is the last
recorded figure — display only, never adopted (like `GET /api/game/bank`).

### `GET /api/game/store` → `200` `StoreDto` · `400` `"Company not found."` · `401` · `403`

```json
{
  "diamonds": 85,
  "cash": 18420.5,
  "boostUntil": "2026-10-09T15:00:00Z",
  "boostMultiplier": 2,
  "maxBoostHours": 24,
  "boosts": [ { "hours": 1, "price": 25 }, { "hours": 3, "price": 60 }, { "hours": 8, "price": 140 } ],
  "doubleOffer": null,
  "diamondValue": 2000,
  "badges": [
    { "id": "unicorn", "icon": "🦄", "name": "Unicorn", "price": 120, "rarity": "epic",
      "owned": true, "featured": true }
  ],
  "featuredBadgeId": "unicorn",
  "history": [
    { "amount": -25, "balance": 85, "reason": "boost", "detail": "1", "createdAt": "2026-10-09T14:00:00Z" }
  ]
}
```

- `badges`: every badge in display order; `rarity` is `common` | `rare` | `epic` | `legendary`.
  `name` is English — clients translate by `id`.
- `history`: the last 20 ledger rows, newest first. `amount` is signed; `balance` is after it;
  `reason` is one of `welcome`, `achievement` (`detail` = achievement code), `prestige`
  (`detail` = new level), `boost` (`detail` = hours), `double-offline`, `exchange`,
  `badge` (`detail` = badge id), `admin` (`detail` = the admin's note), `backfill`.

### `POST /api/game/store/boosts/{hours}` — buy an income boost

No body → `200` `StoreDto` · `400` `"Company not found."` | `"Unknown boost."` |
`"A boost can run at most 24 hours ahead."` | `"Not enough diamonds."`

### `POST /api/game/store/double-offline` — double the last offline earnings

No body → `200` `StoreDto` (cash includes the bonus) · `400` `"Company not found."` |
`"No offline earnings to double."` (none, already used, or expired) | `"Not enough diamonds."`

### `POST /api/game/store/exchange` — diamonds → cash

`{ "diamonds": 10 }` → `200` `StoreDto` · `400` `"Company not found."` |
`"Choose at least 1 diamond."` | `"Not enough diamonds."`

### `POST /api/game/store/badges/{badgeId}` — buy a badge

No body → `200` `StoreDto` · `400` `"Company not found."` | `"Badge not found."` |
`"You already own this badge."` | `"Not enough diamonds."`. The first badge bought becomes
the featured one.

### `PUT /api/game/store/featured-badge` — choose the badge shown next to the name

`{ "badgeId": "unicorn" }` (or `null` to show none) → `200` `StoreDto` · `400`
`"Company not found."` | `"You do not own this badge."`

---

## 7. Admin — catalogue editor — `/api/admin/catalogue`

*Backend shipped 2026-10-02; frontend editor shipped 2026-10-08 in the admin panel
(`/admin/catalogue`) — see `features/002-catalogue-in-database.md` and
`features/006-admin-panel.md`.*

**Bearer required, with the `Admin` role.** The access token carries the role claim
`"http://schemas.microsoft.com/ws/2008/06/identity/claims/role": "Admin"` (the full .NET
claim URI, not a short `role` key — clients reading it should accept both) only when the
player's `IsAdmin` flag is set; it is re-read on every login
and refresh, so a promotion takes effect after the next `/api/auth/refresh` (or login).
The **first** admin is set in the database; after that, admins can promote others with
`PUT /api/admin/players/{id}/role` (§7b):

```sql
UPDATE players SET "IsAdmin" = true WHERE "Email" = 'you@example.com';
```

No token → `401`; a token without the role → `403`. Rate limit `game-actions`.
Every edit applies to businesses opened **after** it (see §5).

### `AdminCatalogueEntryDto`

```json
{
  "id": "food-cart",
  "name": "Food Cart",
  "sector": "Hospitality",
  "requiredPrestige": "TheHustle",
  "openingCost": 500,
  "baseIncomePerSecond": 2,
  "monthlySalaryCost": 0,
  "baseEmployeeCount": 0,
  "description": "A simple street food cart. Low cost, instant income.",
  "displayOrder": 10,
  "isActive": true,
  "createdAt": "2026-10-02T00:00:00Z",
  "updatedAt": "2026-10-02T00:00:00Z",
  "availableAssets": [
    { "id": "menu-item", "name": "Menu item", "price": 200, "unlockAtAssetCount": 0,
      "fixedIncomePerSecond": 0.5, "incomePerSecond": 0.5, "displayOrder": 10 }
  ]
}
```

| Field | Meaning |
|---|---|
| `id` | Slug: lowercase letters, digits and single dashes, ≤ 60 chars. **Immutable** — owned businesses reference it. Asset ids are unique within their business only. |
| `displayOrder` | Sort key within a prestige tier (assets: within the business). Gaps are fine; the seed uses steps of 10. |
| `isActive` | `false` = retired: hidden from players and cannot be opened; existing owners keep it. There is **no delete** — a database foreign key forbids removing an entry somebody owns. |
| `fixedIncomePerSecond` | `null` means income is derived from price: `price × 0.0003` (both ratios, Transport and the rest, currently `0.0003`). |
| `incomePerSecond` | Read-only, computed: what a player buying this asset now would get. |

**Validation** (each a `400` JSON string): `name` required, ≤ 80 chars;
`description` ≤ 500; `openingCost` and asset `price` **> 0**; incomes, salary, employee
count and `unlockAtAssetCount` **≥ 0**; `sector` / `requiredPrestige` must be a known enum
value. An unknown enum *string* is a framework `400` with an empty body.

### `GET /api/admin/catalogue`

→ `200` `AdminCatalogueEntryDto[]` — every entry, retired ones included, ordered as in §5.

### `GET /api/admin/catalogue/{id}`

→ `200` `AdminCatalogueEntryDto` · `404` `"Catalogue entry not found."`

### `POST /api/admin/catalogue`

```json
{
  "id": "lemonade-stand", "name": "Lemonade Stand", "sector": "Hospitality",
  "requiredPrestige": "TheHustle", "openingCost": 100, "baseIncomePerSecond": 1,
  "monthlySalaryCost": 0, "baseEmployeeCount": 0, "description": "…", "displayOrder": 5,
  "availableAssets": [
    { "id": "ice", "name": "Ice box", "price": 50, "unlockAtAssetCount": 0,
      "fixedIncomePerSecond": 0.5, "displayOrder": 10 }
  ]
}
```

`availableAssets` is optional. New entries start active.

→ `201` `AdminCatalogueEntryDto`, `Location: /api/admin/catalogue/{id}` · `400`
`"Catalogue id already exists."` | `"Asset already exists."` | a validation message.

### `PUT /api/admin/catalogue/{id}`

Full replacement of the entry's own fields — same body as `POST` without `id` and
`availableAssets`, **plus `isActive`** (send `false` to retire, `true` to restore).

→ `200` `AdminCatalogueEntryDto` · `400` `"Catalogue entry not found."` | a validation
message.

### `POST /api/admin/catalogue/{id}/assets`

Body: one asset, as in `POST`'s `availableAssets[]`.

→ `200` `AdminCatalogueEntryDto` (the whole entry) · `400` `"Catalogue entry not found."` |
`"Asset already exists."` | a validation message.

### `PUT /api/admin/catalogue/{id}/assets/{assetId}`

Body: the asset without `id`. → `200` `AdminCatalogueEntryDto` · `400`
`"Catalogue entry not found."` | `"Asset not found."` | a validation message.

### `DELETE /api/admin/catalogue/{id}/assets/{assetId}`

Takes the asset off sale. Copies players already bought are untouched — a bought asset
does not reference the catalogue. → `200` `AdminCatalogueEntryDto` · `400`
`"Catalogue entry not found."` | `"Asset not found."`

---

## 7b. Admin — stats, players, manager names — `/api/admin`

Added 2026-10-08 — see `features/006-admin-panel.md`. Same gate as §7: bearer with the
`Admin` role (`401` / `403` otherwise), rate limit `game-actions`.

### `GET /api/admin/stats` → `200` `AdminStatsDto`

```json
{
  "generatedAt": "2026-10-08T12:00:00Z",
  "players": 42, "admins": 1, "newPlayers24h": 3, "newPlayers7d": 12, "activePlayers24h": 9,
  "companies": 40, "totalCash": 1284300.5, "totalAllTimeEarnings": 9874300.75,
  "businessesOwned": 118, "managersOnShift": 17, "averageBusinessLevel": 2.4,
  "prestigeDistribution": [ { "level": "TheHustle", "companies": 30 } ],
  "topBusinesses": [ { "catalogueId": "food-cart", "name": "Food Cart", "owners": 31 } ],
  "catalogueBusinesses": 52, "catalogueActive": 52, "catalogueAssets": 156, "managerNames": 130,
  "luxuryOwned": 23, "luxuryCatalogue": 76, "luxuryCatalogueActive": 76,
  "achievementsUnlocked": 214,
  "achievementDistribution": [ { "code": "earn-1k", "title": "First thousand", "icon": "💵", "companies": 38 } ],
  "loansTaken": 19, "activeLoans": 6, "loansOutstanding": 812400.5, "loansMissedPayments": 3,
  "diamondsInCirculation": 2140, "diamondsEarned": 3900, "diamondsSpent": 1760,
  "badgesOwned": 14, "boostsActive": 2,
  "badgeDistribution": [ { "id": "unicorn", "icon": "🦄", "name": "Unicorn", "price": 120, "owners": 3 } ]
}
```

Every player figure excludes admins (`admins` counts them): `players`, `newPlayers*`, `companies`, cash, earnings, businesses, managers, levels and both lists. `activePlayers24h` = player companies whose `lastSyncAt` is within 24 h. `prestigeDistribution`
lists all 7 levels in order (zeros included). `topBusinesses` = the 5 most-owned catalogue
entries. `achievementDistribution` lists every achievement in display order with how many
player companies unlocked it (zeros included). `activeLoans` / `loansOutstanding` /
`loansMissedPayments` cover loans still being repaid; `loansTaken` counts every loan ever
taken. (Added 2026-10-09: the luxury, achievement and loan fields.) Diamonds (§6e, added 2026-10-09):
`diamondsInCirculation` = player balances; `diamondsEarned` / `diamondsSpent` = positive / negative
ledger totals (admin grants and removals included); `boostsActive` = boosts running now;
`badgeDistribution` lists every badge in display order with its owners (zeros included).

### `GET /api/admin/players?search=` → `200` `AdminPlayerDto[]`

`search` (optional) matches username or email, case-insensitive. Newest first, max 200.

```json
{
  "id": "0f6b2c58-…", "username": "bilel", "email": "bilel@richlife.dev", "country": "TN",
  "isAdmin": false, "createdAt": "2026-10-01T09:00:00Z",
  "companyName": "Oueslati", "cash": 1500.25, "prestigeLevel": "TheHustle",
  "prestigeCount": 0, "allTimeEarnings": 9000, "businesses": 2,
  "lastSeenAt": "2026-10-08T11:58:00Z",
  "highestBusinessLevel": 7, "luxuryOwned": 2, "achievementsUnlocked": 6,
  "loanOutstanding": 17280, "diamonds": 85, "badges": 2
}
```

The company fields are `null` for a player who has not created a company yet.
`loanOutstanding` is what the player still owes on an active loan, `null` without one.
(`highestBusinessLevel`, `luxuryOwned`, `achievementsUnlocked`, `loanOutstanding`, `diamonds` and
`badges` added 2026-10-09.)

### `PUT /api/admin/players/{id}/role`

Body `{ "isAdmin": true }` → `200` `AdminPlayerDto` · `400` `"Player not found."` |
`"You cannot remove your own admin role."`. Takes effect at the player's next login or
token refresh. A promoted player **stops being a player**: their token is refused on
`/api/game/*` and they leave the leaderboard and player stats. Their company is kept
untouched, so demoting them again restores it.

### `PUT /api/admin/players/{id}/cash`

Body `{ "cash": 50000 }` → `200` `AdminPlayerDto` · `400` `"Player not found."` |
`"Player has no company."` | `"Cash cannot be negative."`. Sets cash outright; does not
touch `allTimeEarnings`.

### `POST /api/admin/players/{id}/reset`

No body → `200` `AdminPlayerDto` · `400` `"Player not found."` | `"Player has no company."`.
Fresh start: cash 0, every business removed, `TheHustle`, multiplier ×1, base income 3/s,
`allTimeEarnings` 0, achievements cleared, bank loans deleted, a running boost and the double
offer cancelled. Account, company name, **diamonds and badges** are kept.

**Online players:** a cash change or reset marks the company *overridden*; the player's
next `POST /api/game/sync` ignores the client figure, answers `adjusted: true` with the
admin's value, and clears the mark. The client adopts it and refreshes the company.

### `DELETE /api/admin/players/{id}`

→ `200` (empty) · `400` `"Player not found."` | `"You cannot delete your own account."`.
Deletes the player, their company, businesses and assets.

### `POST /api/admin/players/{id}/forgive-loan`

No body → `200` `AdminPlayerDto` · `400` `"Player not found."` | `"Player has no company."` |
`"Player has no active loan."`. Cancels what is still owed: the loan is closed as **forgiven**
(`LoanDto.forgiven: true`, §6d) and the player can take a new one. Cash is not touched.
Added 2026-10-09.

### `POST /api/admin/players/{id}/diamonds`

`{ "amount": 50, "reason": "Compensation for the outage" }` → `200` `AdminPlayerDto` · `400`
`"Player not found."` | `"Player has no company."` | `"Amount cannot be zero."` |
`"Diamonds cannot go below zero."` | `"Reason must be at most 200 characters."`. A positive
`amount` gives, a negative one takes away; the reason is written to the player's ledger
(`reason: "admin"`). Added 2026-10-09.

### Manager names — `/api/admin/manager-names`

- `GET` → `200` `[ { "id": 1, "name": "Lucy", "inUse": 3 } ]` — `inUse` = businesses
  referencing the name (current or past manager). Alphabetical.
- `POST` body `{ "name": "Biscotte" }` → `201` the new row · `400`
  `"Name is required and must be at most 40 characters."` | `"Name already exists."`.
- `DELETE /{id}` → `200` (empty) · `400` `"Name not found."` |
  `"Name is used by a business."` (a database foreign key protects it).

---

## 7c. Admin — bank — `/api/admin`

Added 2026-10-09 — see `features/010-admin-catch-up.md`. Same gate as §7.

The 20 banks and the offer rules are **rules in code** (`Domain/Banking`), not content, so
they are read-only here — changing a bank's rates is a code change.

### `GET /api/admin/loans?active={bool}` → `200` `AdminLoanDto[]`

`active=true` (the default) lists loans still being repaid; `active=false` lists every loan.
Newest first, max 200. Admins' own loans are included (they cannot take any).

```json
{
  "id": "5b2e…", "playerId": "0f6b…", "username": "bilel", "companyName": "Oueslati",
  "bankId": "carthage-credit", "bankName": "Carthage Credit", "bankIcon": "🏺",
  "principal": 100000, "interestRate": 0.05, "totalRepay": 105000,
  "paid": 26250, "penalties": 0, "outstanding": 78750, "missedPayments": 0,
  "takenAt": "2026-10-09T08:00:00Z", "nextPaymentAt": "2026-10-09T20:00:00Z",
  "repaidAt": null, "forgiven": false
}
```

### `GET /api/admin/banks` → `200` `AdminBankDto[]`

The 20 banks in catalogue order, with their rate and term bands and usage.

```json
{
  "id": "carthage-credit", "name": "Carthage Credit", "icon": "🏺",
  "minRate": 0.04, "maxRate": 0.08, "minInstallments": 4, "maxInstallments": 10,
  "loansTaken": 5, "activeLoans": 1, "totalLent": 412000
}
```

Forgiving a player's loan: `POST /api/admin/players/{id}/forgive-loan` (§7b).

---

## 7d. Admin — luxury catalogue — `/api/admin/luxury`

Added 2026-10-09 — see `features/010-admin-catch-up.md`. Same gate as §7.

The luxury list (§6c) is content in `luxury_catalogue`. Like the business catalogue, an
edit applies to purchases made **after** it — a bought item keeps the name, price and photo
it was bought with. There is **no delete** (a foreign key protects owned items): retire with
`isActive: false`, which hides the item from the shop.

### `AdminLuxuryItemDto`

```json
{
  "id": "rolex-submariner", "name": "Rolex Submariner", "category": "Watch",
  "description": "The diver's watch everyone recognises.", "price": 150000,
  "requiredPrestige": "SmallBusiness",
  "imageUrl": "/luxury/rolex-submariner.jpg", "imageCredit": "Author · CC BY-SA 4.0",
  "imageSourceUrl": "https://commons.wikimedia.org/wiki/File:…",
  "displayOrder": 10, "isActive": true, "owners": 3
}
```

`owners` = companies that bought it. `imageUrl` is a path on the **frontend** origin: a new
photo must be added to `idle-startup-frontend/public/luxury/` (and deployed) before an item
can point at it. Keep the credit and source with the photo — the licenses require it.

**Validation** (each a `400` JSON string): `id` a slug (lowercase letters, digits, single
dashes, ≤ 60); `name` required, ≤ 80; `description` ≤ 300; `price` > 0; `imageUrl`
required, ≤ 300, starting with `/` or `https://`; `imageCredit` ≤ 200; `imageSourceUrl` ≤ 500;
`category` (the 16 values in §6c) and `requiredPrestige` must be known — an unknown enum
*string* is a framework `400` with an empty body.

### `GET /api/admin/luxury` → `200` `AdminLuxuryItemDto[]`

Every item, retired ones included, by required prestige, display order, price.

### `GET /api/admin/luxury/{id}` → `200` `AdminLuxuryItemDto` · `404` `"Item not found."`

### `POST /api/admin/luxury` → `201` `AdminLuxuryItemDto` · `400`

Body: the DTO without `owners` and `isActive` (new items start active). `400`
`"Item id already exists."` or a validation message.

### `PUT /api/admin/luxury/{id}` → `200` `AdminLuxuryItemDto` · `400`

Body: the DTO without `id` and `owners`, **with** `isActive`. `400` `"Item not found."` or a
validation message.

---

## 8. Health — not part of the game API

`GET /health` (all checks) and `GET /alive` (liveness only), both plain text
`Healthy` / `Unhealthy`. Used by Aspire; the frontend does not call them.

---

## 9. Frontend conformance

Status as of 2026-10-09, checked against `idle-startup-frontend/src/app/core/` and the
feature components.

### Resolved

| # | Was | Resolution |
|---|---|---|
| M1 | `/api/game/sync-beacon` could never succeed — `sendBeacon` cannot set an `Authorization` header. | The frontend no longer calls `/sync-beacon`. It flushes with `fetch(..., { keepalive: true })` against `/api/game/sync`, which survives unload *and* can authenticate. Hooked on `visibilitychange` + `pagehide` rather than `beforeunload` (which never fires on mobile). |
| M2 | Ticker omitted `prestigeMultiplier`, so simulated income was 18 % per prestige too low. | `GameService.cashRate` now reads the server's `incomePerSecond` directly — the multiplier is already applied there. |
| M3 | `/api/game/state` was never called; offline earnings were never credited. | `GameService.bootstrap()` calls it on app start and surfaces the result in a "welcome back" dialog. A `404` is treated as "no company yet". |
| M4 | Prestige readiness was checked against `cash` while the server gated on net worth. | Since 2026-10-08 prestige is a cash purchase, so `GameService.canPrestige` compares the live `cash` against `nextPrestigeThreshold`. |
| M5 | Catalogue ↔ owned-business matching was done on `name`. | Matched on `catalogueId` throughout; `BusinessDto.catalogueId` is now part of the frontend model. |
| M6 | Thresholds were hardcoded in `dashboard.component.ts`. | `nextPrestigeThreshold` is read from the response; `GlobalEmpire` is detected by level, not by comparing against `decimal.MaxValue`. |
| M7 | `isOwned` was recomputed client-side. | The server's `isOwned` is used as-is. |
| M8 | `DELETE /api/game/businesses/{id}` had no caller. | Implemented in the manage-business dialog, with `emergency=false` and a confirmation step. |
| M9 | `GET /api/leaderboard/` had no caller. | Implemented as a `/leaderboard` page. |
| M10 | `openBusiness` / `buyAsset` sent a `{}` body. | They now send no body. |
| M11 | The backend DTOs `OpenBusinessRequest` and `BuyAssetRequest` were dead code. | Removed 2026-10-02 with the catalogue move. |
| M12 | §7 (admin catalogue editor) had no frontend. | 2026-10-08: the admin panel (`/admin`, admin-role route guard) covers §7 and §7b. |

### Open

None.

### Deliberate frontend behaviour worth knowing

- **Spend is always preceded by a `/sync`.** `Company.DeductCash` compares the price
  against the server's *last recorded* cash and never accrues on the way, so without a
  sync first the server rejects purchases the player can plainly afford on screen, and
  `allTimeEarnings` (which ranks the leaderboard) loses everything earned since the last
  sync. `GameService` therefore syncs, then spends, then deducts the price locally.
- **`canAfford` is treated as a hint for the button's enabled state.** It is computed
  server-side against that same stale cash, so it lags the live ticker by up to one sync
  interval. The client enables the button when *either* the server flag or the simulated
  purse says it is affordable; the server remains the authority and still answers
  `"Insufficient funds."`. `isOwned` and `isUnlocked` are used verbatim.
- **Bank (§6d).** Take and repay sync first and adopt the response `cash`; a `/sync` carrying
  `loanPayment` adopts `acceptedCash` and shows a bank toast instead of "balance corrected".
- **Store (§6e).** Every store POST syncs first and adopts `cash`, `diamonds` and `boostUntil` from
  the `StoreDto`. The ticker rate is `incomePerSecond × boostMultiplier` while `now < boostUntil`.
- **`GET /api/game/company` is never used to overwrite cash** — it does not accrue, so
  its cash figure is stale by design. It refreshes the structural parts only
  (businesses, net worth, rates).
