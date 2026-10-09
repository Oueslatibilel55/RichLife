import { PrestigeLevel, Sector } from '../../core/models/game.models';
import { LuxuryCategory } from '../luxury/luxury.models';

// Wire contracts for the admin panel — mirror api-contract.md §7, §7b, §7c and §7d.

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
  luxuryOwned: number;
  luxuryCatalogue: number;
  luxuryCatalogueActive: number;
  achievementsUnlocked: number;
  /** Every achievement in display order, zeros included. `title` is English — show `ach.<code>.title`. */
  achievementDistribution: { code: string; title: string; icon: string; companies: number }[];
  /** Every loan ever taken. */
  loansTaken: number;
  /** Loans still being repaid, what they still owe, and their missed payments. */
  activeLoans: number;
  loansOutstanding: number;
  loansMissedPayments: number;
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
  highestBusinessLevel: number | null;
  luxuryOwned: number | null;
  achievementsUnlocked: number | null;
  /** Still owed on an active loan; null without one. */
  loanOutstanding: number | null;
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

// -- Bank (contract §7c) — read-only: banks are rules in code ----------------------

export interface AdminLoan {
  id: string;
  playerId: string;
  username: string;
  companyName: string;
  bankId: string;
  /** Proper noun — not translated. */
  bankName: string;
  bankIcon: string;
  principal: number;
  interestRate: number;
  totalRepay: number;
  paid: number;
  penalties: number;
  outstanding: number;
  missedPayments: number;
  takenAt: string;
  /** null once closed. */
  nextPaymentAt: string | null;
  /** Set once closed — repaid, or forgiven. */
  repaidAt: string | null;
  forgiven: boolean;
}

export interface AdminBank {
  id: string;
  name: string;
  icon: string;
  minRate: number;
  maxRate: number;
  minInstallments: number;
  maxInstallments: number;
  loansTaken: number;
  activeLoans: number;
  totalLent: number;
}

// -- Luxury catalogue (contract §7d) ---------------------------------------------

export interface AdminLuxuryItem {
  id: string;
  name: string;
  category: LuxuryCategory;
  description: string;
  price: number;
  requiredPrestige: PrestigeLevel;
  /** Path on the frontend origin (public/luxury/…) or an https:// URL. */
  imageUrl: string;
  imageCredit: string;
  imageSourceUrl: string;
  displayOrder: number;
  isActive: boolean;
  /** Companies that bought it. */
  owners: number;
}

/** POST body: new items start active. */
export type CreateLuxuryRequest = Omit<AdminLuxuryItem, 'owners' | 'isActive'>;

/** PUT body: everything but the id and owners, with isActive. */
export type UpdateLuxuryRequest = Omit<AdminLuxuryItem, 'id' | 'owners'>;
