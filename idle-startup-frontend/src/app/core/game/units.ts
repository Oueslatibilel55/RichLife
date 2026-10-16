import { BusinessDto, BusinessKind, CompanyDto, PlaceDto, UnitDto, UnitStatus } from '../models/game.models';
import { t } from '../i18n/i18n';

/**
 * Places and breakdowns (contract §5). A unit works while `workingFrom <= now < breaksAt`;
 * both instants come from the server ahead of time, so every "now" figure below is derived on
 * the client clock without a request. The server's numbers are "as of the response" — these
 * helpers correct them by the units whose state changed since.
 */

export function unitStatus(u: UnitDto, nowMs: number): UnitStatus {
  if (nowMs < Date.parse(u.workingFrom)) return 'Repairing';
  return nowMs >= Date.parse(u.breaksAt) ? 'Broken' : 'Working';
}

/** +contribution for a unit that works now but did not at the response, − for the reverse. */
function unitDelta(u: UnitDto, nowMs: number): number {
  const now = unitStatus(u, nowMs) === 'Working' ? 1 : 0;
  const then = u.status === 'Working' ? 1 : 0;
  return (now - then) * u.contributionPerSecond;
}

/** A place's income now (before prestige, like `PlaceDto.incomePerSecond`). */
export function placeIncomeNow(p: PlaceDto, nowMs: number): number {
  return p.incomePerSecond + p.units.reduce((s, u) => s + unitDelta(u, nowMs), 0);
}

/** A business's `netIncomePerSecond` now (before prestige). */
export function businessIncomeNow(b: BusinessDto, nowMs: number): number {
  return b.netIncomePerSecond + businessDelta(b, nowMs);
}

function businessDelta(b: BusinessDto, nowMs: number): number {
  return (b.places ?? []).reduce((s, p) => s + p.units.reduce((x, u) => x + unitDelta(u, nowMs), 0), 0);
}

/** The company's online rate now: `incomePerSecond` corrected by every unit that broke or came back. */
export function companyIncomeNow(c: CompanyDto, nowMs: number): number {
  const delta = c.businesses.reduce((s, b) => s + businessDelta(b, nowMs), 0);
  return c.incomePerSecond + delta * c.prestigeMultiplier;
}

export function brokenUnits(b: BusinessDto, nowMs: number): UnitDto[] {
  return (b.places ?? []).flatMap((p) => p.units.filter((u) => unitStatus(u, nowMs) === 'Broken'));
}

/** Standard price of fixing every broken unit of a business now — what "Repair all" costs. */
export function repairAllCost(b: BusinessDto, nowMs: number): number {
  return brokenUnits(b, nowMs).reduce((s, u) => s + u.repairCost, 0);
}

/** Milliseconds until a repairing unit works again (0 otherwise). */
export function repairMsLeft(u: UnitDto, nowMs: number): number {
  return Math.max(0, Date.parse(u.workingFrom) - nowMs);
}

/** The fault in the player's language, the server's English name as a fallback. */
export function faultLabel(u: UnitDto): string {
  const key = `fault.${u.fault}`;
  const text = t(key);
  return text === key ? u.faultName : text;
}

/** Fleet, site chain or equipment (features/015) — shown on catalogue cards. */
export const KIND_ICONS: Readonly<Record<BusinessKind, string>> = {
  Fleet: '🚚',
  Sites: '🏪',
  Equipment: '🧰',
};

export const TIER_ICONS: Readonly<Record<PlaceDto['tier'], string>> = {
  Quiet: '🌳',
  Busy: '🏙️',
  Prime: '⭐',
};
