import { ChangeDetectionStrategy, Component, computed, inject, input, output, signal } from '@angular/core';
import { GameService } from '../../../core/services/game.service';
import { toErrorMessage } from '../../../core/http/api-error';
import { formatShiftLeft, managerAvatar, shiftMsLeft } from '../../../core/game/managers';
import { MoneyPipe, RatePipe } from '../../../shared/pipes/money.pipe';
import { IconComponent } from '../../../shared/components/icon/icon.component';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';

/**
 * Asset management for one owned business. Split out of DashboardComponent so the
 * dashboard template and stylesheet stay readable (and inside the 8 kB component
 * style budget).
 *
 * Takes the business ID rather than the object so the view re-derives itself from
 * GameService after every refresh instead of holding a stale copy.
 */
@Component({
  selector: 'app-manage-business',
  standalone: true,
  imports: [MoneyPipe, RatePipe, IconComponent, TranslatePipe],
  templateUrl: './manage-business.component.html',
  styleUrl: './manage-business.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ManageBusinessComponent {
  private readonly game = inject(GameService);

  readonly businessId = input.required<string>();
  readonly closed = output<void>();

  readonly busyAssetId = signal<string | null>(null);
  readonly closing = signal(false);
  readonly error = signal('');
  readonly confirmingClose = signal(false);
  readonly hiring = signal(false);
  readonly levelling = signal(false);

  /** Next level that doubles income (10, 25, 50), or null once all are reached. */
  readonly nextMilestone = computed(() => {
    const level = this.business()?.level ?? 1;
    return [10, 25, 50].find((m) => m > level) ?? null;
  });

  /** Income gained per second by the next level, after the prestige multiplier. */
  readonly levelGain = computed(() => {
    const biz = this.business();
    if (!biz || biz.nextLevelIncomePerSecond === null) return 0;
    const multiplier = this.game.company()?.prestigeMultiplier ?? 1;
    return (biz.nextLevelIncomePerSecond - biz.netIncomePerSecond) * multiplier;
  });

  levelUp(): void {
    const biz = this.business();
    if (!biz || biz.nextLevelCost === null || this.levelling()) return;

    this.levelling.set(true);
    this.error.set('');

    this.game.levelUpBusiness(biz.id, biz.nextLevelCost).subscribe({
      next: () => {
        // The company's income rates are server-computed — refresh them.
        this.game.refreshCompany().subscribe({
          next: () => this.levelling.set(false),
          error: () => this.levelling.set(false),
        });
      },
      error: (err: unknown) => {
        this.error.set(toErrorMessage(err, 'manage.error.levelUp'));
        this.levelling.set(false);
      },
    });
  }

  readonly business = computed(
    () => this.game.company()?.businesses.find((b) => b.id === this.businessId()) ?? null,
  );

  /** Matched on `catalogueId` — the display name is not a join key. */
  readonly entry = computed(() => {
    const biz = this.business();
    return biz ? this.game.catalogueEntry(biz.catalogueId) : null;
  });

  readonly cash = this.game.cash;

  /** Standard close refunds 90% of liquidation value, emergency 75%. */
  readonly normalRefund = computed(() => (this.business()?.totalValue ?? 0) * 0.9);

  readonly managerAvatar = managerAvatar;

  /** The nearest locked asset, to show progress toward it ("2 more to unlock Premium dish"). */
  readonly nextUnlock = computed(() => {
    const locked = this.entry()?.availableAssets.filter((a) => !a.isUnlocked) ?? [];
    return locked.reduce<(typeof locked)[number] | null>(
      (best, a) => (!best || a.unlockAtAssetCount < best.unlockAtAssetCount ? a : best),
      null,
    );
  });

  /** `key.one` / `key.other` — the dictionary holds both forms for every counted string. */
  plural(key: string, n: number): string {
    return n === 1 ? `${key}.one` : `${key}.other`;
  }

  /** Assets still to buy before `unlockAt` — never negative. */
  toGo(unlockAt: number): number {
    return Math.max(0, unlockAt - (this.business()?.assetCount ?? 0));
  }

  /** 0..100 progress toward `unlockAt`. */
  progressTo(unlockAt: number): number {
    return unlockAt <= 0 ? 100 : Math.min(100, ((this.business()?.assetCount ?? 0) / unlockAt) * 100);
  }

  /** Live: re-evaluated every second through the service clock. */
  readonly shiftLeft = computed(() => {
    const biz = this.business();
    return biz ? shiftMsLeft(biz, this.game.now()) : 0;
  });
  readonly onShift = computed(() => this.shiftLeft() > 0);
  readonly shiftLeftLabel = computed(() => formatShiftLeft(this.shiftLeft()));

  /** Offline income this business would add once managed — what the player is buying. */
  readonly offlineGain = computed(
    () => (this.business()?.netIncomePerSecond ?? 0) * (this.game.company()?.prestigeMultiplier ?? 1),
  );

  hireManager(): void {
    const biz = this.business();
    if (!biz || this.onShift() || this.hiring()) return;

    this.hiring.set(true);
    this.error.set('');

    this.game.automateBusiness(biz.id, biz.managerCost).subscribe({
      next: () => {
        // Refresh: isAutomated and the company's offlineIncomePerSecond are server-computed.
        this.game.refreshCompany().subscribe({ error: () => void 0 });
        this.hiring.set(false);
      },
      error: (err: unknown) => {
        this.error.set(toErrorMessage(err, 'manage.error.hire'));
        this.hiring.set(false);
      },
    });
  }

  buyAsset(assetId: string, price: number): void {
    const biz = this.business();
    if (!biz || this.busyAssetId()) return;

    this.busyAssetId.set(assetId);
    this.error.set('');

    this.game.buyAsset(biz.id, assetId, price).subscribe({
      next: () => {
        // Refresh both: assetCount and the per-asset `isUnlocked` flags are server-computed.
        this.game.refreshCompany().subscribe({ error: () => void 0 });
        this.game.loadCatalogue().subscribe({ error: () => void 0 });
        this.busyAssetId.set(null);
      },
      error: (err: unknown) => {
        this.error.set(toErrorMessage(err, 'manage.error.buyAsset'));
        this.busyAssetId.set(null);
      },
    });
  }

  closeBusiness(emergency: boolean): void {
    const biz = this.business();
    if (!biz || this.closing()) return;

    this.closing.set(true);
    this.error.set('');

    this.game.closeBusiness(biz.id, emergency).subscribe({
      next: () => {
        this.game.refreshCompany().subscribe({ error: () => void 0 });
        this.game.loadCatalogue().subscribe({ error: () => void 0 });
        this.closing.set(false);
        this.closed.emit();
      },
      error: (err: unknown) => {
        this.error.set(toErrorMessage(err, 'manage.error.close'));
        this.closing.set(false);
      },
    });
  }
}
