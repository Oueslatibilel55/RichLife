import { AvatarDto, PrestigeLevel } from '../../core/models/game.models';
import { OwnedLuxury } from '../luxury/luxury.models';

// Wire contract for GET /api/profile — mirrors api-contract.md §6b.

export interface Profile {
  username: string;
  email: string;
  country: string;
  memberSince: string;
  /** Leaderboard position; null until the player has a company. */
  rank: number | null;
  rankedPlayers: number;
  company: ProfileCompany | null;
  /** Luxury items bought, newest first (contract §6c). */
  luxury: OwnedLuxury[];
  achievementsUnlocked: number;
  achievementsTotal: number;
  achievements: Achievement[];
  /** Badges bought in the store (§6e), newest first. */
  badges: OwnedBadge[];
  /** Shown next to the name (and on the leaderboard); null for none. */
  featuredBadgeId: string | null;
  /** The chosen avatar (§6e); null shows the initial. */
  avatar: AvatarDto | null;
}

export interface OwnedBadge {
  id: string;
  icon: string;
  name: string;
  rarity: 'common' | 'rare' | 'epic' | 'legendary';
  purchasedAt: string;
}

export interface ProfileCompany {
  name: string;
  createdAt: string;
  prestigeLevel: PrestigeLevel;
  prestigeCount: number;
  prestigeMultiplier: number;
  cash: number;
  netWorth: number;
  allTimeEarnings: number;
  incomePerSecond: number;
  businesses: number;
  assets: number;
  managersOnShift: number;
  managersHired: number;
  highestBusinessLevel: number;
  diamonds: number;
  /** Unpaid taxes and every tax ever paid (§5). */
  taxesDue: number;
  taxesPaid: number;
}

export interface Achievement {
  code: string;
  title: string;
  description: string;
  icon: string;
  unlocked: boolean;
  unlockedAt: string | null;
  /** Capped at target once unlocked. */
  current: number;
  target: number;
  unit: 'money' | 'count';
}
