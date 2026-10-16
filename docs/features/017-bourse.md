# 017 — Bourse (stock trading) and the floating menu

**Status:** Done (owner to test on Netlify).

## Why

The owner asked for a stock market to trade on, and for a floating button with a menu to replace
the phone tab bar.

## Bourse — rules (contract §6f)

- 12 fictional stocks (`Domain/Market/StockCatalog`, tickers persisted — never rename).
- Prices step every 30 s (`StockTickSeconds`, ticks aligned on a fixed epoch; `TickStart`, `NextTickAt`). `StockMarket.PriceAt(stock, tick)`: seeded waves (2 h, 9 h, 2.5 d, 11 d; weights 0.12, 0.25,
  0.6, 0.5) + smooth noise (10-minute steps, weight 0.1), × the stock's volatility (0.05–0.18), around
  its base price. Pure function of time: same for everyone, nothing stored, no job, tested.
- Buy/sell whole shares at the minute's price, 1 % fee each way (`GameConstants.StockFeeRate`).
- Holdings (at cost) capped at 24 h of full income, $10,000 at least
  (`StockInvestHours`, `StockMinInvestLimit`) — so trading cannot outgrow the businesses
  (features/016 pacing).
- Realised profit counts toward `AllTimeEarnings`; losses don't reduce it. Net worth counts holdings
  at cost. Owned table `company_stocks` (migration `Bourse`); `AdminReset` clears it.
- `MarketService` + `/api/game/market` (GET, `/{ticker}/history`, `/{ticker}/buy`, `/{ticker}/sell`).

## Frontend

- `features/market/`: portfolio card (value, gain/loss, investing-limit bar), one card per stock with
  a sparkline and 24 h change, and a trade sheet (Day/Week/Month chart, buy/sell, −/+/Max, cost with
  the fee). Prices refresh every 30 s. Trades go through `GameService.syncThenAdoptCash`.
- **Floating menu** (phones, < 900 px): the bottom tab bar and the "More" sheet are gone. A round
  button bottom-end opens a panel: who you are + diamonds + language, every page as a tile (active
  one highlighted), cash/rate and log out. Closes on any navigation. Styles in `styles.scss`
  (`.fab`, `.fab-menu`, layout budget). The desktop top bar now lists the Bourse too.
- i18n: `dict/market.ts` (+ `nav.market`, `nav.menu`), Bourse server messages in `dict/server.ts`.

## Verification

- `dotnet test --solution RichLife.slnx` — 213 passed (8 new in `MarketTests`).
- API on an empty database (all migrations): market list, 24 h change, sparkline; in the browser
  (desktop): page, trade sheet, chart ranges, a buy. The phone menu was not checked visually — the
  test browser would not go below desktop width; check it on the phone.

## Update — a real trading screen (2026-10-16)

- Server: prices step every 30 s; `MarketDto` adds `nextUpdateAt`, `tickSeconds`, the RLX index
  (`indexValue`, `indexChange24h`); each stock adds `change`, `previousClose`, `dayHigh`, `dayLow`,
  `volume` (seeded flavour).
- Page: a dark live board (LIVE badge, RLX, a countdown ring to the next tick), a scrolling ticker
  tape, a quote table (Last, Chg, Chg %, High, Low, Volume, 24 h sparkline — cards on phones) whose
  prices flash green/red when they move, the day's top movers, and a trade sheet with High/Low/
  Prev. close/Volume. The page re-reads the market the second the next tick starts.
- Board and trade-sheet styles live in `styles.scss` (`.bourse`, `.trade-sheet`) — the component
  stylesheet is at its 4 kB budget.
