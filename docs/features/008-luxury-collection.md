# 008 — Luxury collection

**Status:** Done (2026-10-08 — contract §6c, backend and frontend)

## Why

Late-game cash had nothing to be spent on except more of the same. Luxury items are pure
status: a watch, supercars, a villa, a superyacht, an island — with real photos — shown on
the player's profile. Prices are tied to prestige so nothing is in reach at the start.

## The collection (76 items in 16 categories, `luxury_catalogue`)

Shipped with 15 items (watches, motorbikes, cars, property, islands, yachts, aircraft);
expanded the same day with 61 more and 9 new categories: **Jewelry, Fashion, Wine &
spirits, Instruments, Art, Horses, Sports teams, Experiences, Collectibles**.

| Prestige | # | Examples | Prices |
|---|---|---|---|
| P2 | 13 | Rolex Submariner, Hermès Birkin bag, Penny Black stamp, 1959 Gibson Les Paul | 110K – 600K |
| P3 | 14 | Porsche 911 Turbo S, Rare Macallan whisky, Thoroughbred racehorse, Alpine ski chalet | 1.1M – 8M |
| P4 | 14 | Patek Philippe Nautilus, Stradivarius violin, Monet — Water Lilies, Bordeaux wine estate | 12M – 70M |
| P5 | 14 | Superyacht, Suborbital space flight, Van Gogh, Ferrari 250 GTO, Basketball franchise | 120M – 800M |
| P6 | 14 | Bugatti Chiron, Concorde, Vitruvian Man, European football club, Dubai skyscraper | 1.2B – 8B |
| P7 | 7 | Mona Lisa, Gigayacht, Trip around the Moon, Private airport | 12B – 80B |

The database is the list — query `luxury_catalogue` for the full set. `DisplayOrder` runs
10..760, by prestige then price (re-sequenced by the `MoreLuxury` migration).

`LuxuryCategory` is stored as an int — **append only, never renumber** (Watch = 1 …
Collectible = 16). A new category also needs the frontend `LuxuryCategory` union and the
`LUXURY_CATEGORY_ICONS` / `LUXURY_CATEGORY_LABELS` maps in `luxury.models.ts`, plus the
contract §6c list.

## Rules

- Status only — no income. One of each per company (aggregate check + unique index on
  `(CompanyId, CatalogueId)`). Paid from cash; not lost earnings.
- **Counts toward net worth** at its price (`Company.NetWorth` now adds luxury).
- Locked items are listed (aspiration) but cannot be bought below `requiredPrestige`.
- Kept through prestige; an admin reset clears the collection.
- New achievement **"Living large"** (`first-luxury`): own one item — 18 achievements now.

## Photos and licenses

- 76 photos from **Wikimedia Commons**, chosen visually from licensed candidates (CC0,
  public domain, CC BY, CC BY-SA), downloaded once at 960 px into
  `idle-startup-frontend/public/luxury/<id>.jpg`, then recompressed (JPEG via a headless
  Chrome canvas — `sharp` does not load on this Windows box) to ≈ 7 MB in total. Served by
  the frontend, no hotlinking. The paintings are public-domain reproductions.
- Cards crop every photo to a fixed 16:10 frame (`img` absolutely positioned,
  `object-fit: cover`), so a portrait painting does not stretch its card.
- Each row stores `ImageCredit` ("Author · License") and `ImageSourceUrl` (the Commons file
  page). **The credit is shown on every photo** (shop and profile, linked to the source in
  the shop) because CC BY / CC BY-SA require attribution. Keep it if a photo is replaced.
- A bought item copies name, category, price, image and credit (`LuxuryAssets`), like
  businesses copy their catalogue numbers.

## Backend

- Domain: `LuxuryCategory`, `LuxuryCatalogueEntry` (content), `LuxuryAsset` extended
  (CatalogueId, Category, ImageUrl, ImageCredit; it existed but was never created before),
  `Company.BuyLuxury(item, now)`; `NetWorth`, `AdminReset`, achievement metric `LuxuryOwned`.
- `LuxuryService`, `ILuxuryCatalogueRepository`; `GET /api/game/luxury`,
  `POST /api/game/luxury/{id}` (`LuxuryEndpoints`, player policy); profile `luxury` list.
- Migration `20261008132939_LuxuryCollection` — `luxury_catalogue` + seed, new
  `LuxuryAssets` columns, FK (RESTRICT) and unique index.
- Migration `20261008135904_MoreLuxury` — 61 more rows (`InsertData`), then re-sequences
  `DisplayOrder`; `Down` deletes exactly those ids.
- 6 domain tests.

## Frontend

- `/luxury` (nav "Luxury"): category filters, photo cards (category badge, lock, "✓ Yours",
  credit), price, requirement / "Need $X" / Buy → confirmation dialog with the photo and
  cash after. Buying goes through `GameService.spend()` (sync first, deduct locally, refresh
  the company for net worth).
- Profile: **Collection** grid of owned photos with price and date, or an empty state
  linking to the shop. `.photo-credit` is a global style shared by both.
- The player nav has 5 items (Dashboard, Businesses, Luxury, Leaderboard, Profile).

## Verification

- 111/111 domain tests; full solution build 0 warnings; migration applied (15 rows).
- Over HTTP against the real database: P1 → `400 "Requires prestige SmallBusiness."`; P2
  with 200K buys the Rolex (row inserted; cash 50K, net worth 200K); again →
  `"You already own this."`; Ducati with 50K → `"Insufficient funds."`; next sync →
  `first-luxury`; profile lists it; admin → 403.
- Expansion to 76, headless Chrome, real login (16/16): 17 chips (All + 16), 76/76
  photos load and are credited, a P2 player sees 13 buyable and 63 locked, the Rolex is
  bought through the dialog and shows on the profile, no horizontal scroll on phone or
  desktop.
- First release, headless Chrome, real login (14/14): Luxury in the nav; 15/15 photos load, all credited;
  a P2 player sees 2 buyable and 13 locked; Rolex bought through the dialog → "Yours" +
  "Living large" toast; profile collection shows it with its photo; no horizontal scroll.
