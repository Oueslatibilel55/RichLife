import { CORE } from './core';
import { SERVER } from './server';
import { LAYOUT } from './layout';
import { AUTH } from './auth';
import { DASHBOARD } from './dashboard';
import { BUSINESSES } from './businesses';
import { LUXURY } from './luxury';
import { LEADERBOARD } from './leaderboard';
import { PROFILE } from './profile';
import { ADMIN } from './admin';
import { BANK } from './bank';

/** One file per feature, each holding the same keys in all three languages. */
export type Dict = Readonly<Record<string, string>>;
export interface DictSet {
  en: Dict;
  fr: Dict;
  ar: Dict;
}

const PARTS: readonly DictSet[] = [CORE, SERVER, LAYOUT, AUTH, DASHBOARD, BUSINESSES, LUXURY, BANK, LEADERBOARD, PROFILE, ADMIN];

function merge(lang: keyof DictSet): Dict {
  return Object.assign({}, ...PARTS.map((p) => p[lang]));
}

export const DICTIONARY: Readonly<Record<keyof DictSet, Dict>> = {
  en: merge('en'),
  fr: merge('fr'),
  ar: merge('ar'),
};
