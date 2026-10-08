import { PrestigeLevel, Sector } from '../../core/models/game.models';

// Wire contracts for the admin panel — mirror api-contract.md §7 and §7b.

export interface AdminStats {
  generatedAt: string;
  players: number;
  admins: number;
  newPlayers24h: number;
  newPlayers7d: number;
  activePlayers24h: number;
  companies: number;
  totalCash: number;
  totalAllTimeEarnings: number;
  businessesOwned: number;
  managersOnShift: number;
  averageBusinessLevel: number;
  prestigeDistribution: { level: PrestigeLevel; companies: number }[];
  topBusinesses: { catalogueId: string; name: string; owners: number }[];
  catalogueBusinesses: number;
  catalogueActive: number;
  catalogueAssets: number;
  managerNames: number;
}

export interface AdminPlayer {
  id: string;
  username: string;
  email: string;
  country: string;
  isAdmin: boolean;
  createdAt: string;
  /** Company fields are null until the player creates one. */
  companyName: string | null;
  cash: number | null;
  prestigeLevel: PrestigeLevel | null;
  prestigeCount: number | null;
  allTimeEarnings: number | null;
  businesses: number | null;
  lastSeenAt: string | null;
}

export interface ManagerNameRow {
  id: number;
  name: string;
  /** Businesses referencing it (current or past manager); a used name cannot be deleted. */
  inUse: number;
}

export interface AdminCatalogueAsset {
  id: string;
  name: string;
  price: number;
  unlockAtAssetCount: number;
  /** null = derived from price (price × 0.0003). */
  fixedIncomePerSecond: number | null;
  /** Read-only, computed by the server. */
  incomePerSecond: number;
  displayOrder: number;
}

export interface AdminCatalogueEntry {
  id: string;
  name: string;
  sector: Sector;
  requiredPrestige: PrestigeLevel;
  openingCost: number;
  baseIncomePerSecond: number;
  monthlySalaryCost: number;
  baseEmployeeCount: number;
  description: string;
  displayOrder: number;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
  availableAssets: AdminCatalogueAsset[];
}

/** PUT body: the entry's own fields (no id, no assets) plus isActive. */
export type CatalogueEntryFields = Omit<AdminCatalogueEntry, 'id' | 'createdAt' | 'updatedAt' | 'availableAssets'>;

/** Asset POST/PUT body (PUT omits the id). */
export type CatalogueAssetFields = Omit<AdminCatalogueAsset, 'incomePerSecond'>;
