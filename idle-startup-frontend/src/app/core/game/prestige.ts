import { PrestigeLevel } from '../models/game.models';

/**
 * Single source of truth for the prestige enum on the frontend.
 * Adding or renaming a level means editing THIS file only — previously these maps
 * were duplicated across dashboard, layout and businesses.
 */
export const PRESTIGE_ORDER: readonly PrestigeLevel[] = [
  'TheHustle',
  'SmallBusiness',
  'Entrepreneur',
  'BusinessMogul',
  'Tycoon',
  'Billionaire',
  'GlobalEmpire',
] as const;

export const PRESTIGE_LABELS: Readonly<Record<PrestigeLevel, string>> = {
  TheHustle: 'P1 — The Hustle',
  SmallBusiness: 'P2 — Small Business',
  Entrepreneur: 'P3 — Entrepreneur',
  BusinessMogul: 'P4 — Business Mogul',
  Tycoon: 'P5 — Tycoon',
  Billionaire: 'P6 — Billionaire',
  GlobalEmpire: 'P7 — Global Empire',
};

/** Compact form for the HUD. */
export const PRESTIGE_SHORT: Readonly<Record<PrestigeLevel, string>> = {
  TheHustle: 'P1',
  SmallBusiness: 'P2',
  Entrepreneur: 'P3',
  BusinessMogul: 'P4',
  Tycoon: 'P5',
  Billionaire: 'P6',
  GlobalEmpire: 'P7',
};

/** Text-on-white shades (the UI is a light theme); tinted backgrounds append an alpha. */
export const PRESTIGE_COLORS: Readonly<Record<PrestigeLevel, string>> = {
  TheHustle: '#64748B',
  SmallBusiness: '#16A34A',
  Entrepreneur: '#2563EB',
  BusinessMogul: '#D97706',
  Tycoon: '#7C3AED',
  Billionaire: '#DB2777',
  GlobalEmpire: '#0D9488',
};

export function prestigeLabel(level: PrestigeLevel | string): string {
  return PRESTIGE_LABELS[level as PrestigeLevel] ?? level;
}

export function prestigeShort(level: PrestigeLevel | string): string {
  return PRESTIGE_SHORT[level as PrestigeLevel] ?? level;
}

export function prestigeColor(level: PrestigeLevel | string): string {
  return PRESTIGE_COLORS[level as PrestigeLevel] ?? '#64748B';
}

/** Label of the level above `level`, or null at the top. */
export function nextPrestigeLabel(level: PrestigeLevel | string): string | null {
  const i = PRESTIGE_ORDER.indexOf(level as PrestigeLevel);
  if (i < 0 || i >= PRESTIGE_ORDER.length - 1) return null;
  return PRESTIGE_LABELS[PRESTIGE_ORDER[i + 1]!];
}
