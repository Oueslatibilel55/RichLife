import { t } from '../i18n/i18n';

/**
 * Money formatting. Pure functions so they can be unit-tested and memoized by the
 * pipes in shared/pipes — presentation logic does not belong on GameService.
 */

/** Whole-dollar display: $1,284 · $3.21M · $7.90B. */
export function formatCash(n: number): string {
  if (!Number.isFinite(n)) return '$0';
  if (n >= 1_000_000_000) return '$' + (n / 1_000_000_000).toFixed(2) + 'B';
  if (n >= 1_000_000) return '$' + (n / 1_000_000).toFixed(2) + 'M';
  return '$' + Math.floor(n).toLocaleString('en-US');
}

/** Rate / price display, keeps cents: $3.54 · $1,284.00 · $3.21M. */
export function formatRate(n: number): string {
  if (!Number.isFinite(n)) return '$0.00';
  if (n >= 1_000_000_000) return '$' + (n / 1_000_000_000).toFixed(2) + 'B';
  if (n >= 1_000_000) return '$' + (n / 1_000_000).toFixed(2) + 'M';
  if (n >= 1_000) {
    return '$' + n.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
  }
  return '$' + n.toFixed(2);
}

/**
 * .NET `TimeSpan` JSON ("c" format, `[d.]hh:mm:ss[.fffffff]`) -> whole seconds.
 * Past 24 h the server prepends days with a DOT: 30 h is "1.06:00:00", not "30:00:00".
 */
export function timeSpanSeconds(timeSpan: string): number {
  const match = /^(?:(\d+)\.)?(\d+):(\d+):(\d+)/.exec(timeSpan);
  if (!match) return 0;
  const [, d, h, m, s] = match;
  return Number(d ?? 0) * 86_400 + Number(h) * 3_600 + Number(m) * 60 + Number(s);
}

/** "06:12:40" -> "6h 12m" · "1.06:00:00" -> "1d 6h". Used for the offline-earnings panel. */
export function formatElapsed(timeSpan: string): string {
  const total = timeSpanSeconds(timeSpan);
  const d = Math.floor(total / 86_400);
  const h = Math.floor((total % 86_400) / 3_600);
  const m = Math.floor((total % 3_600) / 60);
  if (d > 0) return h > 0 ? t('time.dh', { d, h }) : t('time.d', { d });
  if (h > 0) return t('time.hm', { h, m });
  if (m > 0) return t('time.m', { m });
  return t('time.lessThanMinute');
}
