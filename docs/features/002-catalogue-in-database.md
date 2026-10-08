# 002 — Business catalogue moves to the database, with an admin editor

**Status:** Done — backend 2026-10-02; the Angular editor shipped 2026-10-08 in the admin panel (`006-admin-panel.md`).

## Why

The 17 businesses and 40 assets lived in `Domain/Catalogue/BusinessCatalogue.cs`, so every
price tweak or new business was a code change and a redeploy. They are game *content*, not
rules: they now live in two tables and are edited over `/api/admin/catalogue`, which a
dashboard can sit on.

What did **not** move: `GameConstants` (offline cap, sync tolerance, fee rates, prestige
multiplier, asset income ratios) and the prestige thresholds in `Company`. Those are rules
the domain tests pin; editing them at runtime would make a green suite stop describing the
live game.

## Backend changes (done)

- **Domain** — `BusinessCatalogueEntry` is now its own aggregate (string slug key, no
  `BaseEntity`), owning `AssetCatalogueEntry` children. Admin edits go through
  `Create` / `Update` / `AddAsset` / `UpdateAsset` / `RemoveAsset`, which validate and
  return `Result`. `IsActive` retires an entry; there is no delete.
- **Domain** — `Company.OpenBusiness(entry)` and `Company.BuyAsset(businessId, entry, assetId)`
  took over the prestige, unlock and income rules that used to live in `BusinessService`.
  The asset-income formula moved to `BusinessCatalogueEntry.IncomeFor`.
- **Snapshot semantics** — opening copies the entry's numbers into `Business`, buying copies
  the asset's into `BusinessAsset`. Admin edits therefore affect only future openings and
  purchases. This was already how the data model worked; it is now a documented rule with
  a test.
- **Application** — `ICatalogueRepository`, `CatalogueAdminService`, `CatalogueMapper`,
  admin DTOs. Dead `OpenBusinessRequest` / `BuyAssetRequest` removed (contract M11).
- **Infrastructure** — tables `catalogue_businesses` and `catalogue_assets` (owned,
  composite key `(BusinessCatalogueId, Id)`); FK `businesses.CatalogueId →
  catalogue_businesses.Id` with `ON DELETE RESTRICT`. `CatalogueRepository` caches the whole
  catalogue in `IMemoryCache` (10-minute safety TTL); `CatalogueCacheInvalidator`, a
  `SaveChangesInterceptor`, evicts it after any committed catalogue change.
- **Migration** `20261002161442_MoveCatalogueToDatabase` — creates the tables, adds
  `players.IsAdmin`, seeds the old static catalogue row for row via `InsertData` (not
  `HasData`, so the content does not stay in the model), then adds the FK.
- **Auth** — `players.IsAdmin` → `role: "Admin"` claim on login and refresh; policy `admin`
  on `/api/admin/*`. No endpoint grants admin.

## Frontend changes (pending)

- No player-facing change is required: `BusinessCatalogueDto` is unchanged in shape and
  order. The client already reads everything from `/catalogue` — keep it that way; nothing
  about a business may be hardcoded.
- Admin page (deferred): list from `GET /api/admin/catalogue`, edit form on `PUT`, an
  asset sub-table on the asset routes, and an "active" toggle instead of a delete button.
  Show the page only when the access token carries `role: "Admin"`; the server still
  enforces it (`403`).

## Verification

- `dotnet build RichLife.slnx` — 0 warnings; `dotnet test --solution RichLife.slnx` — 72 green.
- Migration applied to the Aspire dev database: 17 + 40 rows; the FK validated the 5
  existing businesses (`food-cart`, `bike-courier`, `car-wash`); deleting an owned entry
  is refused by the FK.
- Exercised over HTTP: player catalogue (same order as before), open / buy / every
  existing error message; admin `401` / `403`; create (`201`), duplicate and invalid slug
  (`400`), get / `404`; an edit visible to players on the very next request (cache
  eviction); a retired entry hidden and unopenable while its owner can still buy assets; an
  owned business keeping its original opening cost after a price edit; asset add / update /
  remove. Test data removed afterwards.
