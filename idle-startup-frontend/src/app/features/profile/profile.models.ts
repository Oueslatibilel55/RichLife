import { PrestigeLevel } from '../../core/models/game.models';
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
