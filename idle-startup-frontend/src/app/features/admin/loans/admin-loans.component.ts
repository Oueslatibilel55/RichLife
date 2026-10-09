import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { AdminService } from '../admin.service';
import { AdminBank, AdminLoan } from '../admin.models';
import { toErrorMessage } from '../../../core/http/api-error';
import { MoneyPipe } from '../../../shared/pipes/money.pipe';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { t } from '../../../core/i18n/i18n';

type LoanStatus = 'active' | 'repaid' | 'forgiven';

/**
 * Bank loans (contract §7c): every loan, with a "forgive" action on the active ones, and the
 * 20 banks read-only — their rates and terms are rules in code, not content.
 */
@Component({
  selector: 'app-admin-loans',
  standalone: true,
  imports: [DatePipe, MoneyPipe, TranslatePipe],
  templateUrl: './admin-loans.component.html',
  styleUrl: './admin-loans.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AdminLoansComponent implements OnInit {
  private readonly admin = inject(AdminService);

  /** true: loans still being repaid; false: every loan. */
  readonly activeOnly = signal(true);
  readonly loans = signal<AdminLoan[]>([]);
  readonly banks = signal<AdminBank[]>([]);
  readonly loading = signal(true);
  readonly busyId = signal<string | null>(null);
  readonly error = signal('');
  readonly notice = signal('');

  /** The loan waiting for confirmation in the forgive dialog. */
  readonly pending = signal<AdminLoan | null>(null);

  ngOnInit(): void {
    this.load();
    this.admin.getBanks().subscribe({
      next: (rows) => this.banks.set(rows),
      error: (err: unknown) => this.error.set(toErrorMessage(err, 'admin.error.loadBanks')),
    });
  }

  show(activeOnly: boolean): void {
    if (this.activeOnly() === activeOnly) return;
    this.activeOnly.set(activeOnly);
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set('');
    this.admin.getLoans(this.activeOnly()).subscribe({
      next: (rows) => {
        this.loans.set(rows);
        this.loading.set(false);
      },
      error: (err: unknown) => {
        this.error.set(toErrorMessage(err, 'admin.error.loadLoans'));
        this.loading.set(false);
      },
    });
  }

  status(l: AdminLoan): LoanStatus {
    return l.forgiven ? 'forgiven' : l.repaidAt ? 'repaid' : 'active';
  }

  /** 0.045 → "4.5". */
  percent(rate: number): string {
    return String(Math.round(rate * 1000) / 10);
  }

  confirmForgive(): void {
    const loan = this.pending();
    if (!loan) return;
    this.pending.set(null);
    this.busyId.set(loan.id);
    this.error.set('');
    // The API answers with the player's row, not the loan: close the loan here the same way.
    this.admin.forgiveLoan(loan.playerId).subscribe({
      next: () => {
        const closed: AdminLoan = {
          ...loan, outstanding: 0, forgiven: true, nextPaymentAt: null, repaidAt: new Date().toISOString(),
        };
        this.loans.update((rows) =>
          this.activeOnly() ? rows.filter((r) => r.id !== loan.id) : rows.map((r) => (r.id === loan.id ? closed : r)),
        );
        this.banks.update((rows) =>
          rows.map((b) => (b.id === loan.bankId ? { ...b, activeLoans: Math.max(0, b.activeLoans - 1) } : b)),
        );
        this.busyId.set(null);
        this.notice.set(t('admin.loans.forgiven', { name: loan.username }));
        setTimeout(() => this.notice.set(''), 3500);
      },
      error: (err: unknown) => {
        this.busyId.set(null);
        this.error.set(toErrorMessage(err, 'admin.error.action'));
      },
    });
  }
}
