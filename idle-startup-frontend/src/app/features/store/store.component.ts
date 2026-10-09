import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Observable } from 'rxjs';
import { GameService } from '../../core/services/game.service';
import { DiamondTransactionDto, StoreBadgeDto, StoreDto } from '../../core/models/game.models';
import { formatCountdown } from '../../core/game/format';
import { toErrorMessage } from '../../core/http/api-error';
import { currentLang, t } from '../../core/i18n/i18n';
import { MoneyPipe } from '../../shared/pipes/money.pipe';
import { TranslatePipe } from '../../core/i18n/translate.pipe';

type Tab = 'boosts' | 'badges' | 'exchange' | 'history';

const TAB_KEY = 'rl_store_tab';

/**
 * The store (contract §6e): income boosts, profile badges, diamonds → cash, and the diamond
 * history. Diamonds are server truth; every paid action syncs first (GameService.storeAction).
 */
@Component({
  selector: 'app-store',
  standalone: true,
  imports: [FormsModule, MoneyPipe, TranslatePipe],
  templateUrl: './store.component.html',
  styleUrl: './store.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StoreComponent implements OnInit {
  readonly game = inject(GameService);

  readonly tabs: readonly Tab[] = ['boosts', 'badges', 'exchange', 'history'];
  readonly tab = signal<Tab>(readTab());
  readonly loading = signal(true);
  /** What is being bought right now (`boost-1`, `badge-unicorn`, `exchange`, `feature-…`). */
  readonly busy = signal<string | null>(null);
  readonly error = signal('');
  readonly notice = signal('');
  readonly exchangeAmount = signal<number | null>(10);

  readonly store = this.game.store;
  readonly formatCountdown = formatCountdown;

  /** Cash the chosen amount of diamonds would give. */
  readonly exchangePreview = computed(() => {
    const s = this.store();
    const n = Math.floor(this.exchangeAmount() ?? 0);
    return s && n > 0 ? n * s.diamondValue : 0;
  });

  readonly exchangeValid = computed(() => {
    const n = this.exchangeAmount();
    return n !== null && Number.isInteger(n) && n >= 1 && n <= this.game.diamonds();
  });

  ngOnInit(): void {
    this.game.loadStore().subscribe({
      next: () => this.loading.set(false),
      error: (err: unknown) => {
        this.loading.set(false);
        this.error.set(toErrorMessage(err, 'store.loadFailed'));
      },
    });
  }

  select(tab: Tab): void {
    this.tab.set(tab);
    try { localStorage.setItem(TAB_KEY, tab); } catch { /* private mode */ }
  }

  /** Extra cash a boost of `hours` would bring at today's rate (the rate × the hours). */
  boostGain(hours: number): number {
    return (this.game.company()?.incomePerSecond ?? 0) * hours * 3600;
  }

  buyBoost(hours: number): void {
    this.run(`boost-${hours}`, this.game.buyBoost(hours), () => t('store.boostBought', { h: hours }));
  }

  buyBadge(b: StoreBadgeDto): void {
    this.run(`badge-${b.id}`, this.game.buyBadge(b.id), () => t('store.badgeBought', { name: t('badge.' + b.id) }));
  }

  feature(b: StoreBadgeDto | null): void {
    this.run(`feature-${b?.id ?? 'none'}`, this.game.featureBadge(b?.id ?? null),
      () => (b ? t('store.badgeFeatured', { name: t('badge.' + b.id) }) : t('store.badgeHidden')));
  }

  setExchange(n: number): void {
    this.exchangeAmount.set(Math.max(1, Math.min(n, this.game.diamonds())));
  }

  exchange(): void {
    const n = this.exchangeAmount();
    if (!this.exchangeValid() || n === null) return;
    const cash = this.exchangePreview();
    this.run('exchange', this.game.exchangeDiamonds(n), () => t('store.exchanged', { n, cash: formatMoney(cash) }));
  }

  /** "Achievement — Millionaire", "Badge — Unicorn", "Boost — 3 h"… */
  reasonLabel(h: DiamondTransactionDto): string {
    const base = t('store.reason.' + h.reason);
    switch (h.reason) {
      case 'achievement': return h.detail ? `${base} — ${t('ach.' + h.detail + '.title')}` : base;
      case 'badge': return h.detail ? `${base} — ${t('badge.' + h.detail)}` : base;
      case 'boost': return h.detail ? `${base} — ${t('store.hours', { h: h.detail })}` : base;
      case 'prestige': return h.detail ? `${base} — ${t('prestige.' + h.detail)}` : base;
      case 'admin': return h.detail ? `${base} — ${h.detail}` : base;
      default: return base;
    }
  }

  reasonIcon(h: DiamondTransactionDto): string {
    return REASON_ICONS[h.reason] ?? '💎';
  }

  date(iso: string): string {
    const d = new Date(iso);
    return Number.isNaN(d.getTime())
      ? ''
      : d.toLocaleString(currentLang(), { day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' });
  }

  private run(key: string, op: Observable<StoreDto>, success: () => string): void {
    if (this.busy()) return;
    this.busy.set(key);
    this.error.set('');
    this.notice.set('');
    op.subscribe({
      next: () => {
        this.busy.set(null);
        this.notice.set(success());
      },
      error: (err: unknown) => {
        this.busy.set(null);
        this.error.set(toErrorMessage(err, 'store.error'));
      },
    });
  }
}

const REASON_ICONS: Readonly<Record<string, string>> = {
  welcome: '🎁',
  achievement: '🏅',
  prestige: '🏆',
  boost: '⚡',
  'double-offline': '✨',
  exchange: '💱',
  badge: '🎖️',
  admin: '🛡️',
  backfill: '🎁',
};

function readTab(): Tab {
  try {
    const v = localStorage.getItem(TAB_KEY);
    if (v === 'boosts' || v === 'badges' || v === 'exchange' || v === 'history') return v;
  } catch { /* private mode */ }
  return 'boosts';
}

function formatMoney(n: number): string {
  return '$' + Math.floor(n).toLocaleString('en-US');
}
