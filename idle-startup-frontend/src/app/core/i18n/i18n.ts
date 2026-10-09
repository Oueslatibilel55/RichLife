import { signal } from '@angular/core';
import { DICTIONARY } from './dict';

/**
 * Runtime UI language. A plain module-level signal rather than only a service, so pure
 * helpers (toErrorMessage, prestigeLabel, formatElapsed) can translate too. Any template
 * or computed() that calls `t()` reads the signal and re-renders when the language changes.
 */
export type Lang = 'en' | 'fr' | 'ar';

export const LANGS: readonly { code: Lang; label: string }[] = [
  { code: 'en', label: 'English' },
  { code: 'fr', label: 'Français' },
  { code: 'ar', label: 'العربية' },
];

const STORAGE_KEY = 'rl_lang';

function initialLang(): Lang {
  try {
    const saved = localStorage.getItem(STORAGE_KEY);
    if (saved === 'en' || saved === 'fr' || saved === 'ar') return saved;
  } catch {
    // localStorage throws in private-mode Safari — fall through to the browser language.
  }
  const nav = (typeof navigator !== 'undefined' ? navigator.language : 'en').slice(0, 2);
  return nav === 'fr' || nav === 'ar' ? nav : 'en';
}

const _lang = signal<Lang>(initialLang());
export const currentLang = _lang.asReadonly();

export function setLang(lang: Lang): void {
  _lang.set(lang);
  try {
    localStorage.setItem(STORAGE_KEY, lang);
  } catch {
    // Not persisted — the choice still applies to this session.
  }
  applyToDocument(lang);
}

/** `<html lang dir>` — Arabic flips the whole layout right-to-left. */
export function applyToDocument(lang: Lang = _lang()): void {
  if (typeof document === 'undefined') return;
  document.documentElement.lang = lang;
  document.documentElement.dir = lang === 'ar' ? 'rtl' : 'ltr';
}

/**
 * Translates `key`, falling back to English, then to the key itself.
 * `{name}` placeholders are filled from `params`.
 */
export function t(key: string, params?: Readonly<Record<string, string | number>>): string {
  const lang = _lang();
  const text = DICTIONARY[lang][key] ?? DICTIONARY.en[key] ?? key;
  if (!params) return text;
  return text.replace(/\{(\w+)\}/g, (m, p: string) => (p in params ? String(params[p]) : m));
}

/** True when `key` exists in the English dictionary — used to translate server messages. */
export function hasKey(key: string): boolean {
  return key in DICTIONARY.en;
}
