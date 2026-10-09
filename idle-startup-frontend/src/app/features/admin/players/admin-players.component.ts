import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Observable } from 'rxjs';
import { AdminService } from '../admin.service';
import { AdminPlayer } from '../admin.models';
import { AuthService } from '../../../core/services/auth.service';
import { prestigeColor, prestigeShort } from '../../../core/game/prestige';
import { toErrorMessage } from '../../../core/http/api-error';
import { MoneyPipe } from '../../../shared/pipes/money.pipe';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { t } from '../../../core/i18n/i18n';

type PendingAction =
  | { kind: 'cash'; player: AdminPlayer }
  | { kind: 'reset'; player: AdminPlayer }
  | { kind: 'delete'; player: AdminPlayer };

@Component({
  selector: 'app-admin-players',
  standalone: true,
  imports: [FormsModule, DatePipe, MoneyPipe, TranslatePipe],
  templateUrl: './admin-players.component.html',
  styleUrl: './admin-players.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AdminPlayersComponent implements OnInit {
  private readonly admin = inject(AdminService);
  private readonly auth = inject(AuthService);

  readonly players = signal<AdminPlayer[]>([]);
  readonly loading = signal(true);
  readonly busyId = signal<string | null>(null);
  readonly error = signal('');
  readonly notice = signal('');
  readonly search = signal('');

  readonly pending = signal<PendingAction | null>(null);
  readonly cashInput = signal<number | null>(null);

  readonly prestigeShort = prestigeShort;
  readonly prestigeColor = prestigeColor;

  /** Your own row: the API refuses demoting or deleting yourself, so the UI does too. */
  readonly myId = computed(() => this.auth.currentUser()?.playerId ?? null);

  private searchTimer: ReturnType<typeof setTimeout> | null = null;

  ngOnInit(): void {
    this.load();
  }

  onSearch(value: string): void {
    this.search.set(value);
    if (this.searchTimer) clearTimeout(this.searchTimer);
    this.searchTimer = setTimeout(() => this.load(), 300);
  }

  load(): void {
    this.loading.set(true);
    this.admin.players(this.search()).subscribe({
      next: (rows) => {
        this.players.set(rows);
        this.loading.set(false);
      },
      error: (err: unknown) => {
        this.error.set(toErrorMessage(err, 'admin.error.loadPlayers'));
        this.loading.set(false);
      },
    });
  }

  toggleAdmin(p: AdminPlayer): void {
    this.run(p, this.admin.setAdmin(p.id, !p.isAdmin),
      p.isAdmin
        ? t('admin.players.demoted', { name: p.username })
        : t('admin.players.promoted', { name: p.username }));
  }

  ask(kind: PendingAction['kind'], player: AdminPlayer): void {
    this.cashInput.set(kind === 'cash' ? Math.floor(player.cash ?? 0) : null);
    this.pending.set({ kind, player });
  }

  confirm(): void {
    const action = this.pending();
    if (!action) return;
    const p = action.player;
    this.pending.set(null);

    switch (action.kind) {
      case 'cash': {
        const cash = Number(this.cashInput());
        if (!Number.isFinite(cash) || cash < 0) {
          this.error.set(t('admin.players.cashInvalid'));
          return;
        }
        this.run(p, this.admin.setCash(p.id, cash), t('admin.players.cashSet', { name: p.username }));
        break;
      }
      case 'reset':
        this.run(p, this.admin.resetPlayer(p.id), t('admin.players.wasReset', { name: p.username }));
        break;
      case 'delete':
        this.busyId.set(p.id);
        this.admin.deletePlayer(p.id).subscribe({
          next: () => {
            this.players.update((rows) => rows.filter((r) => r.id !== p.id));
            this.done(t('admin.players.deleted', { name: p.username }));
          },
          error: (err: unknown) => this.fail(err),
        });
        break;
    }
  }

  /** Runs an action that returns the updated row, and swaps it into the list. */
  private run(p: AdminPlayer, call: Observable<AdminPlayer>, message: string): void {
    this.busyId.set(p.id);
    this.error.set('');
    call.subscribe({
      next: (updated) => {
        this.players.update((rows) => rows.map((r) => (r.id === updated.id ? updated : r)));
        this.done(message);
      },
      error: (err: unknown) => this.fail(err),
    });
  }

  private done(message: string): void {
    this.busyId.set(null);
    this.notice.set(message);
    setTimeout(() => this.notice.set(''), 3500);
  }

  private fail(err: unknown): void {
    this.busyId.set(null);
    this.error.set(toErrorMessage(err, 'admin.error.action'));
  }
}
