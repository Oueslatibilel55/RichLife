import { PrestigeLevel } from '../../core/models/game.models';

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

/** Display names — the wire values are enum names ("SportsTeam"). */
export const LUXURY_CATEGORY_LABELS: Readonly<Record<LuxuryCategory, string>> = {
  Watch: 'Watches',
  Motorbike: 'Motorbikes',
  Car: 'Cars',
  Property: 'Property',
  Island: 'Islands',
  Yacht: 'Yachts',
  Aircraft: 'Aircraft',
  Jewelry: 'Jewelry',
  Fashion: 'Fashion',
  Wine: 'Wine & spirits',
  Instrument: 'Instruments',
  Art: 'Art',
  Horse: 'Horses',
  SportsTeam: 'Sports teams',
  Experience: 'Experiences',
  Collectible: 'Collectibles',
};
