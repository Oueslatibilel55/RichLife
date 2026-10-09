import { BusinessDto } from '../models/game.models';
import { t } from '../i18n/i18n';

/**
 * A stable avatar per manager name, so "Lucy" always looks the same wherever she
 * appears. Pure presentation: the name itself comes from the server.
 */
const AVATARS = ['🧑‍💼', '👩‍💼', '👨‍💼', '🧑‍🍳', '👩‍🔧', '🧑‍💻', '👩‍🎨', '🧑‍🚀', '👨‍🔬', '👩‍🏫'] as const;

export function managerAvatar(name: string | null): string {
  if (!name) return AVATARS[0];
  let hash = 0;
  for (const ch of name) hash = (hash * 31 + ch.charCodeAt(0)) >>> 0;
  return AVATARS[hash % AVATARS.length]!;
}

/**
 * Milliseconds left in the manager's 4-hour shift at `now`; 0 when none is running. Use this
 * (with `GameService.now`) rather than `isAutomated`, which is only "as of the response".
 */
export function shiftMsLeft(biz: Pick<BusinessDto, 'managerUntil'>, now: number): number {
  if (!biz.managerUntil) return 0;
  return Math.max(0, Date.parse(biz.managerUntil) - now);
}

/** 9_000_000 ms -> "2h 30m" · 600_000 -> "10m" · under a minute -> "<1m". */
export function formatShiftLeft(ms: number): string {
  const minutes = Math.floor(ms / 60_000);
  const h = Math.floor(minutes / 60);
  const m = minutes % 60;
  if (h > 0) return t('time.hm', { h, m });
  return m > 0 ? t('time.m', { m }) : t('dashboard.shiftUnderMinute');
}
