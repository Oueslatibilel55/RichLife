import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { GameService } from '../../core/services/game.service';
import { toErrorMessage } from '../../core/http/api-error';
import { MoneyPipe, RatePipe } from '../../shared/pipes/money.pipe';
import { IconComponent } from '../../shared/components/icon/icon.component';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { MarketService } from './market.service';
import { HistoryRange, MarketDto, StockDto, linePath } from './market.models';

/** How often the page re-reads prices — they move every minute on the server. */
const REFRESH_MS = 30_000;

/**
 * The Bourse (contract §6f): every stock with its sparkline, the portfolio, and a trade sheet
 * with a chart. Prices come from the server; the page never invents one.
 */
@Component({
  selector: 'app-market',
  standalone: true,
  imports: [FormsModule, MoneyPipe, RatePipe, IconComponent, TranslatePipe],
  templateUrl: './market.component.html',
  styleUrl: './market.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MarketComponent implements OnInit {
  private readonly market = inject(MarketService);
  private readonly game = inject(GameService);

  readonly data = signal<MarketDto | null>(null);
  readonly error = signal('');
  readonly cash = this.game.cash;

  /** The stock whose trade sheet is open. */
  readonly selectedTicker = signal<string | null>(null);
  readonly side = signal<'buy' | 'sell'>('buy');
  readonly range = signal<HistoryRange>('day');
  readonly chart = signal<number[]>([]);
  readonly trading = signal(false);
  readonly tradeError = signal('');
  shares = 1;

  readonly linePath = linePath;
  readonly ranges: HistoryRange[] = ['day', 'week', 'month'];

  readonly selected = computed(() => this.data()?.stocks.find((s) => s.ticker === this.selectedTicker()) ?? null);

  /** Held shares, worth now, and the gain or loss against what they cost. */
  readonly profit = computed(() => {
    const d = this.data();
    return d ? d.portfolioValue - d.invested : 0;
  });

  /** The chart is green when the range ends at or above where it started. */
  readonly chartUp = computed(() => {
    const c = this.chart();
    return c.length < 2 || c[c.length - 1]! >= c[0]!;
  });

  readonly held = computed(() => this.data()?.stocks.filter((s) => s.shares > 0) ?? []);

  /** Share of the investing limit already used, 0..100. */
  readonly limitUsed = computed(() => {
    const d = this.data();
    return d && d.investLimit > 0 ? Math.min(100, (d.invested / d.investLimit) * 100) : 0;
  });

  constructor() {
    const timer = setInterval(() => this.refresh(), REFRESH_MS);
    inject(DestroyRef).onDestroy(() => clearInterval(timer));
  }

  ngOnInit(): void {
    this.refresh();
  }

  refresh(): void {
    this.market.load().subscribe({
      next: (d) => {
        this.data.set(d);
        this.error.set('');
      },
      error: (err: unknown) => this.error.set(toErrorMessage(err, 'market.error.load')),
    });
  }

  open(stock: StockDto): void {
    this.selectedTicker.set(stock.ticker);
    this.side.set('buy');
    this.shares = 1;
    this.tradeError.set('');
    this.loadChart();
  }

  close(): void {
    this.selectedTicker.set(null);
  }

  setRange(range: HistoryRange): void {
    this.range.set(range);
    this.loadChart();
  }

  private loadChart(): void {
    const ticker = this.selectedTicker();
    if (!ticker) return;
    this.chart.set([]);
    this.market.history(ticker, this.range()).subscribe({
      next: (h) => this.chart.set(h.points.map((p) => p.price)),
      error: () => this.chart.set(this.selected()?.spark ?? []),
    });
  }

  /** Most shares the player could buy now: cash and the investing limit, fee included. */
  maxBuy(s: StockDto): number {
    const d = this.data();
    if (!d) return 0;
    const room = Math.min(this.cash(), d.investLimit - d.invested);
    return Math.max(0, Math.floor(room / (s.price * (1 + d.feeRate))));
  }

  /** What the trade costs (buy) or pays (sell), fee included. */
  estimate(s: StockDto): number {
    const fee = this.data()?.feeRate ?? 0;
    const n = Math.max(0, Math.floor(this.shares || 0));
    return s.price * n * (this.side() === 'buy' ? 1 + fee : 1 - fee);
  }

  setShares(n: number): void {
    this.shares = Math.max(1, Math.floor(n));
  }

  trade(): void {
    const s = this.selected();
    const n = Math.floor(this.shares || 0);
    if (!s || n < 1 || this.trading()) return;
    this.trading.set(true);
    this.tradeError.set('');
    const call = this.side() === 'buy' ? this.market.buy(s.ticker, n) : this.market.sell(s.ticker, n);
    call.subscribe({
      next: (d) => {
        this.data.set(d);
        this.trading.set(false);
        this.shares = 1;
      },
      error: (err: unknown) => {
        this.tradeError.set(toErrorMessage(err, 'market.error.trade'));
        this.trading.set(false);
      },
    });
  }

  percent(fraction: number): string {
    const p = fraction * 100;
    return `${p >= 0 ? '+' : ''}${p.toFixed(2)}%`;
  }
}
