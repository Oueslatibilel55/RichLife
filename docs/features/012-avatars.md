# 012 — Avatars (profile pictures)

**Status:** Done (2026-10-09 — contract §6e, plus `avatar` on §2 `CompanyDto`, §6 leaderboard, §6b profile, §7b stats; backend, frontend, admin)

## Why

Players had only their initial as a picture. An avatar personalises the profile and the
leaderboard, and gives diamonds (011) another thing worth saving for.

## Rules (in code — `Domain/Store/AvatarCatalog.cs`)

- **22 avatars**, each an emoji on a two-colour gradient (`from` → `to`).
- **Free**: 4 (Smiley, Cool, Cat, Dog). Everyone can use them, and they are never "bought".
- **For diamonds**: 30 / 60 / 120 / 250 💎. Rarity follows the price on the same scale as badges (`StoreRarity`).
- **Bought once, kept for good, and put on straight away.** The player can switch to any owned or free avatar, or back to their initial (`null`).
- **Avatar ids are stored: never rename or reuse one.** An id that disappears from the catalogue simply shows the initial.
- Buying writes a diamond ledger line (`reason: "avatar"`, `detail` = id). An admin reset keeps avatars, like badges.

## API (contract §6e)

- `POST /api/game/store/avatars/{avatarId}` buys an avatar and puts it on. Errors: `"Avatar not found."`, `"You already own this avatar."` (free ones included), `"Not enough diamonds."`.
- `PUT /api/game/store/avatar` with `{ "avatarId": "fox" | null }` chooses the avatar in use. Errors: `"Avatar not found."`, `"You do not own this avatar."`. No money moves.
- `StoreDto.avatars` (each with `owned` and `selected`) and `StoreDto.avatarId`.
- `AvatarDto { id, icon, from, to }` on `CompanyDto.avatar`, `ProfileDto.avatar` and every leaderboard entry.
- Admin stats: `avatarsOwned`, plus `avatarDistribution` with `owners` (bought) and `inUse`.

## Backend

- `Company.AvatarId`; owned `Avatars` (`company_avatars`, key `(CompanyId, AvatarId)`); `OwnsAvatar`, `BuyAvatar`, `SelectAvatar`. Entity `CompanyAvatar`.
- `StoreService.BuyAvatarAsync` / `SelectAvatarAsync`, and the static `StoreService.Avatar(id)` used by the mapper, the profile and the leaderboard.
- Migration `Avatars` adds one column and one table.
- Tests: 4 more in `StoreTests`, **171** green in total.

## Frontend

- `shared/components/avatar` (`<app-avatar [avatar] [name] [size]>`) shows the emoji on its gradient, or the initial on the brand gradient.
- **Where avatars appear:**
  - The top-bar user button and the "More" sheet.
  - The profile hero: tapping it opens `/store?tab=avatars`, with a ✏️ hint.
  - Every leaderboard row.
- **Store "Avatars" tab:**
  - A preview of the current avatar and a "Use my initial" button.
  - A grid of all avatars with Free / rarity labels and Use / In use / Buy.
  - `?tab=` selects a tab.
- **`GameService`:**
  - `avatar` is computed from the company.
  - `buyAvatar` syncs first, like every paid store action.
  - `selectAvatar` does not sync and updates the company's avatar locally.
- **Admin overview:** an "Avatars bought" tile, and avatars in use / bought per avatar.
- **i18n:** `avatar.<id>` names and the store keys in `dict/store.ts`; three server messages in `dict/server.ts`. All in en/fr/ar.

## Verification (2026-10-09)

- **Backend tests:** 171 green; migration applied to the Aspire database.
- **Over HTTP, with a temporary player:**
  - The store lists 22 avatars, 4 of them owned for free.
  - Selecting the free Cat works, and buying it is refused.
  - Selecting an avatar the player doesn't own is refused.
  - Buying Fox with 25 💎 fails; after raising the balance to 100, Fox is bought (70 left) and put on.
  - The avatar shows on the company, the profile and the leaderboard.
  - Choosing `null` and then choosing Fox again both work.
  - The ledger shows `-30 avatar fox`.
  - Unknown ids are refused.
- **Admin stats:** checked with a temporary admin.
- **Cleanup:** both temporary accounts were deleted.
- **Frontend:** production build clean, i18n parity 0 problems. Not checked in a browser.
