# 007 — Player profile and achievements

**Status:** Done (2026-10-08 — contract §6b + `/sync` `newAchievements`, backend and frontend)

## Why

Players had no page about themselves: no rank at a glance, no summary of their company, and
nothing to aim for between prestiges. The profile gives them all three, with 17
achievements as short-term goals.

## What the player gets

- **`/profile`** (game nav "Profile", and the avatar/name in the top bar): avatar, username,
  country, member since, **leaderboard rank** ("#3 of 12", links to the leaderboard), a
  company summary (all-time earned, net worth, income, multiplier, prestiges, businesses,
  assets, managers on shift / hired, best business level), and the **achievements** grid —
  progress bar + "current / target" while locked, unlock date once earned; filter All /
  Unlocked / Locked.
- **"🏆 Achievement unlocked" toast** at the top of the game within one sync (≤ 5 s) of
  earning one; it links to the profile and fades after 6 s.

## Rules

- 17 achievements over 7 metrics (all-time earnings, cash on hand, businesses owned, assets
  owned, managers hired, highest business level, prestige count), defined in code in
  `Domain/Achievements/Achievements.cs` (`AchievementCatalog`) — rules, like `GameConstants`.
  **Codes are stored: never rename or reuse one.**
- Checked on every `/sync` and on `GET /api/profile`; saved **once** with the time first seen
  (`company_achievements`), so an unlock is permanent even if the metric drops. Announced in
  exactly one sync response. An admin reset clears them; prestige keeps them.
- No rewards yet (a cash reward per achievement is the obvious follow-up).
- Admins have no profile (`/api/profile` is behind the player policy → 403).

## Backend

- Domain: `AchievementCatalog`, `AchievementMetric`, `CompanyAchievement` (owned by
  `Company`), `Company.AchievementMetricValue`, `Company.UnlockAchievements(now)`;
  `AdminReset` clears them.
- `CompanyService.SyncAsync` → `SyncResultDto.NewAchievements`; `ProfileService` +
  `GET /api/profile` (`ProfileEndpoints`, `AuthPolicies.Player`);
  `ILeaderboardRepository.GetRankAsync` (rank among non-admin players with a company).
- EF: `OwnsMany(Achievements)` → table `company_achievements` (PK `CompanyId, Code`, cascade
  with the company). Migration `20261008124128_CompanyAchievements`.
- 6 domain tests (none on a new company, unlock once with its time, permanent after the
  metric drops, manager + level metrics, admin reset clears, unique codes).

## Frontend

- `features/profile/` — `ProfileComponent`, `ProfileService`, `profile.models.ts`; route
  `/profile` under the player layout.
- `GameService.achievementToasts` (queued from `/sync`, auto-dismissed) rendered by
  `LayoutComponent`; toast styles are global (`styles.scss`) to keep the layout under its
  style budget. The tab bar now sizes its columns to the item count (4 for players).

## Verification

- 105/105 domain tests; full solution build 0 warnings; migration applied.
- Over HTTP against the real database: profile without a company (`company: null`, rank
  null, 0/17); opening a business → the next sync returns `first-business` once, the row is
  **inserted** in `company_achievements` (owned-collection additions persist), the profile
  shows rank 2 of 4 and 1/17; admin → 403; admin reset deletes the rows.
- Headless Chrome, real login (16/16): the toast appears after opening a business through
  the UI; the profile shows player, company, rank, "1 / 17", the unlock date and money
  progress (`$80 / $1,000`) on phone and desktop with no horizontal scroll.
