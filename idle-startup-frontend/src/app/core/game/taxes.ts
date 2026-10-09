import { BusinessDto } from '../models/game.models';

/** Milliseconds until this business's current tax period is billed (0 once it is due). */
export function taxMsLeft(biz: BusinessDto, nowMs: number): number {
  return Math.max(0, Date.parse(biz.taxPeriodEndsAt) - nowMs);
}
