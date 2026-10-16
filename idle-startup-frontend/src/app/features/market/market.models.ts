/** The Bourse (contract §6f, features/017-bourse.md). */

export interface MarketDto {
  /** The server's cash after the call — adopted after a trade (which syncs first). */
  cash: number;
  /** Broker fee on every buy and sell (0.01). */
  feeRate: number;
  /** What the held shares cost, fees included. */
  invested: number;
  /** The most that may be held, at cost (one day of income, $10K at least). */
  investLimit: number;
  /** What the held shares are worth now. */
  portfolioValue: number;
  /** Start of the price tick the figures are for. */
  pricedAt: string;
  stocks: StockDto[];
  /** When prices change next (UTC ISO) — every `tickSeconds` (30). */
  nextUpdateAt: string;
  tickSeconds: number;
  /** RLX: every stock against its base price, × 1,000. */
  indexValue: number;
  indexChange24h: number;
}

export interface StockDto {
  ticker: string;
  /** A fictional company — not translated. */
  name: string;
  sector: string;
  icon: string;
  price: number;
  /** Change over 24 h, as a fraction (0.034 = +3.4 %). */
  change24h: number;
  /** 24 hourly prices, oldest first, for the sparkline. */
  spark: number[];
  shares: number;
  costBasis: number;
  value: number;
  /** Price − previousClose (the price 24 h ago). */
  change: number;
  previousClose: number;
  dayHigh: number;
  dayLow: number;
  /** Shares traded over 24 h — flavour, not a rule. */
  volume: number;
}

export type HistoryRange = 'day' | 'week' | 'month';

export interface StockHistoryDto {
  ticker: string;
  range: HistoryRange;
  points: { at: string; price: number }[];
}

/** SVG path of a line through `values`, fitted to a w × h box (2 px padding). */
export function linePath(values: number[], w: number, h: number): string {
  if (values.length < 2) return '';
  const min = Math.min(...values);
  const max = Math.max(...values);
  const span = max - min || 1;
  return values
    .map((v, i) => {
      const x = (i / (values.length - 1)) * w;
      const y = 2 + (1 - (v - min) / span) * (h - 4);
      return `${i ? 'L' : 'M'}${x.toFixed(1)},${y.toFixed(1)}`;
    })
    .join(' ');
}
