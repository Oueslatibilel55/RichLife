import { Pipe, PipeTransform } from '@angular/core';
import { formatCash, formatRate } from '../../core/game/format';

/** Whole-dollar display. Pure, so static prices are memoized across ticks. */
@Pipe({ name: 'money', standalone: true, pure: true })
export class MoneyPipe implements PipeTransform {
  transform(value: number | null | undefined): string {
    return formatCash(value ?? 0);
  }
}

/** Rate / price display, keeps cents. */
@Pipe({ name: 'rate', standalone: true, pure: true })
export class RatePipe implements PipeTransform {
  transform(value: number | null | undefined): string {
    return formatRate(value ?? 0);
  }
}
