import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { GameService } from '../../core/services/game.service';
import { toErrorMessage } from '../../core/http/api-error';
import { formatShiftLeft } from '../../core/game/managers';
import { formatCash } from '../../core/game/format';
import { currentLang, t } from '../../core/i18n/i18n';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { MoneyPipe } from '../../shared/pipes/money.pipe';
import { LoanOfferDto } from '../../core/models/game.models';

/** Reload a little after `offersRefreshAt`, so the server has surely rotated them. */
const ROTATE_GRACE_MS = 2_000;

/**
 * Bank / loans (contract §6d). The data lives in `GameService.bank` so an installment
 * collected by /sync refreshes this page too. Take and repay sync first and adopt the
 * server's cash (GameService); this component only drives the dialogs.
 */
@Component({
  selector: 'app-bank',
  standalone: true,
  imports: [MoneyPipe, TranslatePipe],
  templateUrl: './bank.component.html',
  styleUrl: './bank.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BankComponent implements OnInit {
  readonly game = inject(GameService);

  readonly bank = this.game.bank;
  readonly loading = signal(true);
  readonly error = signal('');
  readonly notice = signal('');
  readonly confirmingOffer = signal<LoanOfferDto | null>(null);
  readonly confirmingRepay = signal(false);
  readonly busy = signal(false);

  readonly loan = computed(() => this.bank()?.activeLoan ?? null);

  /** paid / (totalRepay + penalties), in %. */
  readonly progress = computed(() => {
    const l = this.loan();
    if (!l) return 0;
    const due = l.totalRepay + l.penalties;
    return due > 0 ? Math.min(100, (l.paid / due) * 100) : 100;
  });

  readonly nextPaymentIn = computed(() => {
    const at = this.loan()?.nextPaymentAt;
    if (!at) return '';
    const ms = Date.parse(at) - this.game.now();
    return ms > 0 ? formatShiftLeft(ms) : t('bank.active.dueNow');
  });

  readonly offersRefreshIn = computed(() => {
    const at = this.bank()?.offersRefreshAt;
    if (!at) return '';
    const ms = Date.parse(at) - this.game.now();
    return ms > 0 ? t('bank.offers.refresh', { time: formatShiftLeft(ms) }) : t('bank.offers.refreshing');
  });

  /** Live purse vs what is owed — the server still decides (it answers "Insufficient funds."). */
  readonly canRepay = computed(() => {
    const l = this.loan();
    return !!l && this.game.cash() >= l.outstanding;
  });

  private rotateTimer: ReturnType<typeof setTimeout> | null = null;

  constructor() {
    inject(DestroyRef).onDestroy(() => this.clearRotateTimer());
  }

  ngOnInit(): void {
    this.game.ensureLoaded().subscribe({ error: () => void 0 });
    this.load();
  }

  load(): void {
    this.game.loadBank().subscribe({
      next: (b) => {
        this.loading.set(false);
        this.scheduleRotation(b.offersRefreshAt);
      },
      error: (err: unknown) => {
        this.error.set(toErrorMessage(err, 'bank.loadError'));
        this.loading.set(false);
      },
    });
  }

  /** Offers rotate every 6 h: reload them once `offersRefreshAt` has passed. */
  private scheduleRotation(at: string): void {
    this.clearRotateTimer();
    const ms = Date.parse(at) - Date.now() + ROTATE_GRACE_MS;
    // setTimeout caps at ~24.8 days; offers rotate every 6 h, so this is always in range.
    this.rotateTimer = setTimeout(() => this.load(), Math.max(ROTATE_GRACE_MS, ms));
  }

  private clearRotateTimer(): void {
    if (this.rotateTimer) clearTimeout(this.rotateTimer);
    this.rotateTimer = null;
  }

  percent(rate: number): string {
    return String(Math.round(rate * 1000) / 10);
  }

  date(iso: string | null): string {
    return iso ? new Date(iso).toLocaleDateString(currentLang()) : '';
  }

  take(): void {
    const offer = this.confirmingOffer();
    if (!offer || this.busy()) return;
    this.busy.set(true);
    this.error.set('');
    this.notice.set('');

    this.game.takeLoan(offer.id).subscribe({
      next: () => {
        this.confirmingOffer.set(null);
        this.busy.set(false);
        this.notice.set(t('bank.take.done', { amount: formatCash(offer.amount) }));
      },
      error: (err: unknown) => {
        this.error.set(toErrorMessage(err, 'bank.takeError'));
        this.confirmingOffer.set(null);
        this.busy.set(false);
        // Rotated offers or a loan taken elsewhere: show the bank as it is now.
        if (isBusinessFailure(err)) this.load();
      },
    });
  }

  repay(): void {
    if (this.busy()) return;
    this.busy.set(true);
    this.error.set('');
    this.notice.set('');

    this.game.repayLoan().subscribe({
      next: () => {
        this.confirmingRepay.set(false);
        this.busy.set(false);
        this.notice.set(t('bank.repay.done'));
      },
      error: (err: unknown) => {
        this.error.set(toErrorMessage(err, 'bank.repayError'));
        this.confirmingRepay.set(false);
        this.busy.set(false);
        if (isBusinessFailure(err)) this.load();
      },
    });
  }
}

/** A 400 with a message ("This offer has expired.", "You already have a loan…"). */
function isBusinessFailure(err: unknown): boolean {
  return err instanceof HttpErrorResponse && err.status === 400;
}

