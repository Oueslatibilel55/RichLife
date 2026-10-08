/**
 * Wire contracts for /api/game/* and /api/leaderboard.
 * Mirrors docs/api-contract.md §2 — keep the two in step, contract first.
 */

export type PrestigeLevel =
  | 'TheHustle'
  | 'SmallBusiness'
  | 'Entrepreneur'
  | 'BusinessMogul'
  | 'Tycoon'
  | 'Billionaire'
  | 'GlobalEmpire';

export type Sector =
  | 'Transport'
  | 'RealEstate'
  | 'StockMarket'
  | 'TechStartup'
  | 'Hospitality'
  | 'Energy'
  | 'Services';

export interface CompanyDto {
  id: string;
  name: string;
  cash: number;
  passiveIncomePerSecond: number;
  /** Online rate — the prestige multiplier is ALREADY applied. This is what the ticker simulates. */
  incomePerSecond: number;
  /** Same formula, automated businesses only. Used while the tab is closed. */
  offlineIncomePerSecond: number;
  netWorth: number;
  /**
   * Cumulative lifetime income — what `/api/leaderboard` ranks on. Never decreases
   * and survives prestige.
   *
   * OPTIONAL until the backend maps it: see docs/features/001-all-time-earnings-on-company.md.
   */
  allTimeEarnings?: number;
  prestigeLevel: PrestigeLevel;
  prestigeCount: number;
  prestigeMultiplier: number;
  /** Net worth required for the NEXT prestige. decimal.MaxValue at GlobalEmpire. */
  nextPrestigeThreshold: number;
  lastSyncAt: string;
  businesses: BusinessDto[];
}

export interface BusinessDto {
  id: string;
  /** Join key back to the catalogue — match on this, never on `name`. */
  catalogueId: string;
  name: string;
  sector: Sector;
  requiredPrestige: PrestigeLevel;
  openingCost: number;
  /** Gross minus amortized salary, BEFORE the prestige multiplier. */
  netIncomePerSecond: number;
  /** Liquidation value — what a close refunds, minus the fee. */
  totalValue: number;
  /** Has a manager: keeps earning while the player is away. */
  isAutomated: boolean;
  /** Price of hiring a manager (2 × openingCost). Meaningless once `isAutomated`. */
  managerCost: number;
  /** Current or last manager's first name ("Lucy"), picked server-side; null until first hire. */
  managerName: string | null;
  /** End of the current/last 4-hour shift (UTC ISO). Compare with the clock — `isAutomated` goes stale. */
  managerUntil: string | null;
  /** 1..100. Each level +10 % income; levels 10, 25 and 50 double it. */
  level: number;
  /** Already applied to `netIncomePerSecond`. */
  levelMultiplier: number;
  /** Price of the next level; null at max level. */
  nextLevelCost: number | null;
  /** `netIncomePerSecond` after the next level; null at max level. */
  nextLevelIncomePerSecond: number | null;
  isForSale: boolean;
  askingPrice: number | null;
  assetCount: number;
}

export interface AssetCatalogueDto {
  id: string;
  name: string;
  price: number;
  incomePerSecond: number;
  /** Assets (any type) the business must own first — for "2 more to unlock" against `BusinessDto.assetCount`. */
  unlockAtAssetCount: number;
  /** Server-computed against the caller's business — the authority. Not recomputed client-side. */
  isUnlocked: boolean;
}

export interface BusinessCatalogueDto {
  id: string;
  name: string;
  sector: Sector;
  requiredPrestige: PrestigeLevel;
  openingCost: number;
  baseIncomePerSecond: number;
  monthlySalaryCost: number;
  baseEmployeeCount: number;
  description: string;
  /** Server-computed against the server's LAST RECORDED cash, so it lags the live ticker. */
  canAfford: boolean;
  /** Server-computed. Authoritative — do not recompute by matching names. */
  isOwned: boolean;
  availableAssets: AssetCatalogueDto[];
}

/** GET /api/game/state — the bootstrap call. Credits time spent away. */
export interface OfflineEarningsDto {
  /** TimeSpan as "hh:mm:ss". */
  elapsed: string;
  earned: number;
  cashBefore: number;
  cashAfter: number;
  /** true when `elapsed` exceeded the 4-hour accrual cap. */
  capped: boolean;
  company: CompanyDto;
}

/** POST /api/game/sync — `adjusted` means the client figure was clamped. */
export interface SyncResultDto {
  acceptedCash: number;
  adjusted: boolean;
  /** Achievements unlocked by this sync — each reported exactly once (contract §4 /sync). */
  newAchievements: AchievementUnlocked[];
}

export interface AchievementUnlocked {
  code: string;
  title: string;
  icon: string;
}

export interface LeaderboardEntryDto {
  rank: number;
  username: string;
  country: string;
  companyName: string;
  allTimeEarnings: number;
  prestigeLevel: PrestigeLevel;
  prestigeCount: number;
}
