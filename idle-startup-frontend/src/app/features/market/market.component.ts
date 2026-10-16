import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { GameService } from '../../core/services/game.service';
import { toErrorMessage } from '../../core/http/api-error';
import { MoneyPipe, RatePipe } from '../../shared/pipes/money.pipe';
import { IconComponent } from '../../shared/components/icon/icon.component';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { MarketService } from './market.service';
import { HistoryRange, MarketDto, StockDto, linePath } from './market.models';

/**
 * The Bourse (contract §6f), laid out like a trading screen: a live header with the RLX index
 * and a countdown to the next price tick (every 30 s, server-side), a scrolling ticker tape, a
 * quote table whose prices flash when they move, the portfolio, and a trade sheet with a chart.
 * Prices come from the server; the page never invents one.
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

  /** Wall clock for the countdown, every second. */
  readonly now = signal(Date.now());
  /** Ticker → 'up' | 'down' for a moment after its price moved. */
  readonly flash = signal<Record<string, 'up' | 'down'>>({});
  private loading = false;

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

  readonly limitUsed = computed(() => {
    const d = this.data();
    return d && d.investLimit > 0 ? Math.min(100, (d.invested / d.investLimit) * 100) : 0;
  });

  /** Seconds to the next price tick, and how much of the tick has gone (0..1) for the ring. */
  readonly secondsLeft = computed(() => {
    const d = this.data();
    return d ? Math.max(0, Math.ceil((Date.parse(d.nextUpdateAt) - this.now()) / 1000)) : 0;
  });
  readonly tickProgress = computed(() => {
    const d = this.data();
    return d ? 1 - Math.min(1, this.secondsLeft() / d.tickSeconds) : 0;
  });

  readonly chartUp = computed(() => {
    const c = this.chart();
    return c.length < 2 || c[c.length - 1]! >= c[0]!;
  });

  /** Biggest movers of the day, for the header. */
  readonly gainers = computed(() => [...(this.data()?.stocks ?? [])].sort((a, b) => b.change24h - a.change24h).slice(0, 3));

  constructor() {
    const timer = setInterval(() => {
      this.now.set(Date.now());
      // A new tick has started on the server: fetch it (one request per tick at most).
      const d = this.data();
      if (d && Date.now() >= Date.parse(d.nextUpdateAt) + 300) this.refresh();
    }, 1000);
    inject(DestroyRef).onDestroy(() => clearInterval(timer));
  }

  ngOnInit(): void {
    this.refresh();
  }

  refresh(): void {
    if (this.loading) return;
    this.loading = true;
    this.market.load().subscribe({
      next: (d) => {
        this.loading = false;
        this.apply(d);
        this.error.set('');
      },
      error: (err: unknown) => {
        this.loading = false;
        this.error.set(toErrorMessage(err, 'market.error.load'));
      },
    });
  }

  /** Adopts a fresh market and flashes every price that moved since the last one. */
  private apply(d: MarketDto): void {
    const before = this.data();
    if (before) {
      const moved: Record<string, 'up' | 'down'> = {};
      for (const s of d.stocks) {
        const old = before.stocks.find((o) => o.ticker === s.ticker);
        if (old && old.price !== s.price) moved[s.ticker] = s.price > old.price ? 'up' : 'down';
      }
      this.flash.set(moved);
      setTimeout(() => this.flash.set({}), 1200);
    }
    this.data.set(d);
    if (this.selectedTicker() && this.range() === 'day') this.loadChart();
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
    this.market.history(ticker, this.range()).subscribe({
      next: (h) => this.chart.set(h.points.map((p) => p.price)),
      error: () => this.chart.set(this.selected()?.spark ?? []),
    });
  }

  maxBuy(s: StockDto): number {
    const d = this.data();
    if (!d) return 0;
    const room = Math.min(this.cash(), d.investLimit - d.invested);
    return Math.max(0, Math.floor(room / (s.price * (1 + d.feeRate))));
  }

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
        this.apply(d);
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

  signed(n: number): string {
    return `${n >= 0 ? '+' : '−'}${Math.abs(n).toFixed(2)}`;
  }

  /** 1 234 567 → "1.23M". */
  compact(n: number): string {
    if (n >= 1e9) return `${(n / 1e9).toFixed(2)}B`;
    if (n >= 1e6) return `${(n / 1e6).toFixed(2)}M`;
    if (n >= 1e3) return `${(n / 1e3).toFixed(1)}K`;
    return String(n);
  }
}
