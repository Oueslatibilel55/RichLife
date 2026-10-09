import { PrestigeLevel } from '../../core/models/game.models';
import { t } from '../../core/i18n/i18n';

// Wire contracts for /api/game/luxury — mirror api-contract.md §6c.

export type LuxuryCategory =
  | 'Watch' | 'Motorbike' | 'Car' | 'Property' | 'Island' | 'Yacht' | 'Aircraft'
  | 'Jewelry' | 'Fashion' | 'Wine' | 'Instrument' | 'Art' | 'Horse' | 'SportsTeam' | 'Experience' | 'Collectible';

export interface LuxuryItem {
  id: string;
  name: string;
  category: LuxuryCategory;
  description: string;
  price: number;
  requiredPrestige: PrestigeLevel;
  /** Static file on this (frontend) origin, e.g. "/luxury/superyacht.jpg". */
  imageUrl: string;
  /** "Author · License" — must be shown with the photo. */
  imageCredit: string;
  imageSourceUrl: string;
  isUnlocked: boolean;
  isOwned: boolean;
  /** Against the server's last recorded cash — a hint, like the business catalogue. */
  canAfford: boolean;
}

export interface OwnedLuxury {
  id: string;
  name: string;
  category: LuxuryCategory;
  price: number;
  imageUrl: string;
  imageCredit: string;
  purchasedAt: string;
}

export const LUXURY_CATEGORY_ICONS: Readonly<Record<LuxuryCategory, string>> = {
  Watch: '⌚',
  Motorbike: '🏍️',
  Car: '🏎️',
  Property: '🏛️',
  Island: '🏝️',
  Yacht: '🛥️',
  Aircraft: '✈️',
  Jewelry: '💍',
  Fashion: '👜',
  Wine: '🍷',
  Instrument: '🎻',
  Art: '🖼️',
  Horse: '🐎',
  SportsTeam: '🏟️',
  Experience: '🚀',
  Collectible: '🏺',
};

/** Display-name translation keys — the wire values are enum names ("SportsTeam"). */
export const LUXURY_CATEGORY_LABELS: Readonly<Record<LuxuryCategory, string>> = {
  Watch: 'luxury.cat.Watch',
  Motorbike: 'luxury.cat.Motorbike',
  Car: 'luxury.cat.Car',
  Property: 'luxury.cat.Property',
  Island: 'luxury.cat.Island',
  Yacht: 'luxury.cat.Yacht',
  Aircraft: 'luxury.cat.Aircraft',
  Jewelry: 'luxury.cat.Jewelry',
  Fashion: 'luxury.cat.Fashion',
  Wine: 'luxury.cat.Wine',
  Instrument: 'luxury.cat.Instrument',
  Art: 'luxury.cat.Art',
  Horse: 'luxury.cat.Horse',
  SportsTeam: 'luxury.cat.SportsTeam',
  Experience: 'luxury.cat.Experience',
  Collectible: 'luxury.cat.Collectible',
};

/** Translated category name; an unknown (newly added) category shows its raw enum name. */
export function luxuryCategoryLabel(category: string): string {
  const key = LUXURY_CATEGORY_LABELS[category as LuxuryCategory];
  return key ? t(key) : category;
}
