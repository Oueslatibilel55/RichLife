import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import {
  Observable,
  TimeoutError,
  catchError,
  finalize,
  of,
  retry,
  switchMap,
  tap,
  throwError,
  timeout,
  timer,
} from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuthService } from './auth.service';
import { timeSpanSeconds } from '../game/format';
import {
  AchievementUnlocked,
  BankDto,
  BusinessCatalogueDto,
  BusinessDto,
  CompanyDto,
  LeaderboardEntryDto,
  LoanPaymentDto,
  OfflineEarningsDto,
  SyncResultDto,
} from '../models/game.models';

/** Ticker cadence. 20 frames/s is smooth enough and keeps change detection cheap. */
const TICK_MS = 50;
/** Server sync cadence. Well inside the 20-requests/10s `game-actions` budget. */
const SYNC_MS = 5_000;
/** A reload or a quick tab switch is not "time away" — no welcome-back dialog below this. */
const MIN_AWAY_SECONDS = 60;
/**
 * Loading the game after time away. The deployed API sleeps after ~15 idle minutes and
 * takes 30–60 s to wake; meanwhile Netlify's proxy answers 502/504 and a phone that just
 * woke up can leave a request hanging forever. So each attempt is cut off, and the load
 * is retried for about a minute before the caller gives up (the layout then sends the
 * player back to the login page).
 */
const LOAD_ATTEMPT_MS = 15_000;
const LOAD_RETRIES = 3;
const LOAD_RETRY_DELAY_MS = 3_000;

/**
 * The single source of game truth. Deliberately stateful and long-lived: it survives
 * navigation, so components must NOT re-bootstrap it blindly — call `ensureLoaded()`.
 *
 * Two facts about the backend drive most of the design here:
 *
 *  1. `incomePerSecond` from the server ALREADY includes the prestige multiplier, so
 *     the ticker simulates that number directly rather than re-deriving it.
 *  2. The server deducts purchases from its LAST RECORDED cash and never accrues on
 *     the way (`Company.DeductCash`). Its cash therefore lags the live ticker, so every
 *     spend is preceded by a `/sync` — otherwise the server rejects purchases the
 *     player can plainly afford on screen, and `allTimeEarnings` (which ranks the
 *     leaderboard) silently loses everything earned since the last sync.
 */
@Injectable({ providedIn: 'root' })
export class GameService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly api = environment.apiUrl;

  // -- State ----------------------------------------------------------------

  private readonly _company = signal<CompanyDto | null>(null);
  private readonly _cash = signal(0);
  private readonly _catalogue = signal<BusinessCatalogueDto[]>([]);
  private readonly _offlineEarnings = signal<OfflineEarningsDto | null>(null);
  /** False until /game/state has answered once — distinguishes "loading" from "no company". */
  private readonly _loaded = signal(false);
  /** True while the first load is being retried — the server is probably waking up. */
  private readonly _wakingServer = signal(false);
  private readonly _syncAdjusted = signal(false);
  private syncWarningTimer: ReturnType<typeof setTimeout> | null = null;
  /** Wall clock, refreshed once a second by the ticker — drives countdowns (manager shifts). */
  private readonly _now = signal(Date.now());
  /** Achievements announced by /sync, shown as toasts until dismissed or timed out. */
  private readonly _achievementToasts = signal<AchievementUnlocked[]>([]);
  /** The bank page's data (§6d) — null until `loadBank()`; refreshed when an installment is collected. */
  private readonly _bank = signal<BankDto | null>(null);
  /** Installment collected by /sync (or /state without the welcome-back dialog) — a 6 s toast. */
  private readonly _bankToast = signal<LoanPaymentDto | null>(null);
  private bankToastTimer: ReturnType<typeof setTimeout> | null = null;

  readonly company = this._company.asReadonly();
  readonly cash = this._cash.asReadonly();
  readonly catalogue = this._catalogue.asReadonly();
  readonly offlineEarnings = this._offlineEarnings.asReadonly();
  readonly loaded = this._loaded.asReadonly();
  readonly wakingServer = this._wakingServer.asReadonly();
  /** True when the server last clamped our figure — surfaced as a gentle warning. */
  readonly syncAdjusted = this._syncAdjusted.asReadonly();
  readonly now = this._now.asReadonly();
  readonly achievementToasts = this._achievementToasts.asReadonly();
  readonly bank = this._bank.asReadonly();
  readonly bankToast = this._bankToast.asReadonly();

  // -- Derived --------------------------------------------------------------

  /** Online rate with the prestige multiplier already applied (contract section 2). */
  readonly cashRate = computed(() => this._company()?.incomePerSecond ?? 0);
  readonly offlineRate = computed(() => this._company()?.offlineIncomePerSecond ?? 0);

  /** Live net worth: server net worth with the simulated cash swapped in for its stale cash. */
  readonly netWorth = computed(() => {
    const c = this._company();
    if (!c) return 0;
    return this._cash() + (c.netWorth - c.cash);
  });

  readonly isMaxPrestige = computed(() => this._company()?.prestigeLevel === 'GlobalEmpire');

  /** Cash price of the next prestige — straight from the server, never hardcoded. */
  readonly prestigeThreshold = computed(() => this._company()?.nextPrestigeThreshold ?? 0);

  /** Prestige is a CASH purchase (contract section 4) — business value cannot pay for it. */
  readonly canPrestige = computed(
    () => !this.isMaxPrestige() && this._cash() >= this.prestigeThreshold(),
  );

  readonly neededForPrestige = computed(() =>
    Math.max(0, this.prestigeThreshold() - this._cash()),
  );

  readonly prestigeProgress = computed(() => {
    const threshold = this.prestigeThreshold();
    if (this.isMaxPrestige() || threshold <= 0) return 100;
    return Math.min(100, (this._cash() / threshold) * 100);
  });

  /** Catalogue ids the player already owns — matched by id, never by display name. */
  readonly ownedCatalogueIds = computed(
    () => new Set(this._company()?.businesses.map((b) => b.catalogueId) ?? []),
  );

  // -- Loop handles ---------------------------------------------------------

  private tickHandle: ReturnType<typeof setInterval> | null = null;
  private syncHandle: ReturnType<typeof setInterval> | null = null;
  private lastTickAt = 0;
  private resuming = false;
  private unloadHooked = false;

  // -- Bootstrap ------------------------------------------------------------

  /**
   * The correct app-start call: GET /game/state credits time spent away (capped at
   * 4 h, automated businesses only) and moves the server watermark, so it cannot
   * double-credit with /sync. A 404 simply means "this player has no company yet".
   *
   * Only a success or a 404 marks the game as loaded. Any other failure must NOT: with
   * `loaded` true and no company, the dashboard offers to create one — which is what a
   * player with a company saw when the sleeping server answered 502 after time away.
   */
  bootstrap(): Observable<OfflineEarningsDto | null> {
    return this.http.get<OfflineEarningsDto>(`${this.api}/game/state`).pipe(
      timeout(LOAD_ATTEMPT_MS),
      retry({
        count: LOAD_RETRIES,
        delay: (err: unknown, attempt: number) => {
          if (!isTransient(err)) return throwError(() => err);
          this._wakingServer.set(true);
          return timer(LOAD_RETRY_DELAY_MS * attempt);
        },
      }),
      tap((res) => {
        this.applyCompany(res.company);
        this._loaded.set(true);
        const dialog = res.earned > 0 && timeSpanSeconds(res.elapsed) >= MIN_AWAY_SECONDS;
        if (dialog) this._offlineEarnings.set(res);
        // Installments collected while away: a line in the welcome-back dialog, else a toast.
        if (res.loanPayment) {
          if (!dialog) this.flashBankToast(res.loanPayment);
          this.reloadBankIfLoaded();
        }
      }),
      catchError((err: unknown) => {
        if (err instanceof HttpErrorResponse && err.status === 404) {
          this._company.set(null);
          this._loaded.set(true);
          return of(null);
        }
        return throwError(() => err);
      }),
      finalize(() => this._wakingServer.set(false)),
    );
  }

  /** Reuse the live state when it is already there; only bootstrap when it is not. */
  ensureLoaded(): Observable<OfflineEarningsDto | null> {
    if (this._company()) {
      this.startLoops();
      return of(null);
    }
    return this.bootstrap();
  }

  dismissOfflineEarnings(): void {
    this._offlineEarnings.set(null);
  }

  dismissSyncWarning(): void {
    if (this.syncWarningTimer) clearTimeout(this.syncWarningTimer);
    this.syncWarningTimer = null;
    this._syncAdjusted.set(false);
  }

  /** Informational only: shows the "balance corrected" toast for 4 s, restarting on a repeat. */
  private flashSyncWarning(): void {
    if (this.syncWarningTimer) clearTimeout(this.syncWarningTimer);
    this._syncAdjusted.set(true);
    this.syncWarningTimer = setTimeout(() => this.dismissSyncWarning(), 4000);
  }

  dismissBankToast(): void {
    if (this.bankToastTimer) clearTimeout(this.bankToastTimer);
    this.bankToastTimer = null;
    this._bankToast.set(null);
  }

  /** Announces a collected installment for 6 s — replaces the generic "balance corrected" toast. */
  private flashBankToast(payment: LoanPaymentDto): void {
    if (this.bankToastTimer) clearTimeout(this.bankToastTimer);
    this.dismissSyncWarning();
    this._bankToast.set(payment);
    this.bankToastTimer = setTimeout(() => this.dismissBankToast(), 6000);
  }

  dismissAchievement(code: string): void {
    this._achievementToasts.update((list) => list.filter((a) => a.code !== code));
  }

  /** Queues achievement toasts; each disappears on its own after a few seconds. */
  private announce(unlocked: AchievementUnlocked[]): void {
    this._achievementToasts.update((list) => [...list, ...unlocked]);
    for (const a of unlocked) setTimeout(() => this.dismissAchievement(a.code), 6000);
  }

  // -- Company --------------------------------------------------------------

  createCompany(name: string): Observable<CompanyDto> {
    return this.http
      .post<CompanyDto>(`${this.api}/game/company`, { companyName: name })
      .pipe(tap((c) => this.applyCompany(c)));
  }

  /**
   * Read-only refresh. Does NOT accrue and does not move `lastSyncAt`, so the cash it
   * returns is the server's stale figure — we keep simulating locally and adopt only
   * the structural parts (businesses, net worth, rates).
   */
  refreshCompany(): Observable<CompanyDto> {
    return this.http
      .get<CompanyDto>(`${this.api}/game/company`)
      .pipe(tap((c) => this._company.set(c)));
  }

  // -- Sync -----------------------------------------------------------------

  /**
   * Reports the locally simulated cash. The server clamps it to
   * `lastRecordedCash + incomePerSecond * elapsed * 1.05`; when it does, `adjusted` is
   * true and the contract requires us to adopt `acceptedCash` as the new truth.
   */
  syncNow(): Observable<SyncResultDto> {
    return this.http
      .post<SyncResultDto>(`${this.api}/game/sync`, { cash: Math.floor(this._cash()) })
      .pipe(
        tap((res) => {
          if (res.loanPayment) {
            // The bank collected an installment: `acceptedCash` is net of it (so `adjusted`
            // is true) — announce the payment, not a generic correction.
            this._cash.set(res.acceptedCash);
            this.flashBankToast(res.loanPayment);
            this.refreshCompany().subscribe({ error: () => void 0 });
            this.reloadBankIfLoaded();
          } else if (res.adjusted) {
            this._cash.set(res.acceptedCash);
            this.flashSyncWarning();
            // An admin may have changed or reset the company — refresh its structure too.
            this.refreshCompany().subscribe({ error: () => void 0 });
          }
          if (res.newAchievements?.length) this.announce(res.newAchievements);
        }),
      );
  }

  /** Sync, then run `op`. A failed sync must not block the action. */
  private afterSync<T>(op: () => Observable<T>): Observable<T> {
    return this.syncNow().pipe(
      catchError(() => of(null)),
      switchMap(op),
    );
  }

  // -- Businesses -----------------------------------------------------------

  loadCatalogue(): Observable<BusinessCatalogueDto[]> {
    return this.http
      .get<BusinessCatalogueDto[]>(`${this.api}/game/businesses/catalogue`)
      .pipe(tap((list) => this._catalogue.set(list)));
  }

  /** Catalogue entry for an owned business, matched on `catalogueId`. */
  catalogueEntry(catalogueId: string): BusinessCatalogueDto | null {
    return this._catalogue().find((c) => c.id === catalogueId) ?? null;
  }

  openBusiness(catalogueId: string): Observable<BusinessDto> {
    const cost = this.catalogueEntry(catalogueId)?.openingCost ?? 0;
    return this.afterSync(() =>
      this.http
        .post<BusinessDto>(`${this.api}/game/businesses/${catalogueId}`, null)
        .pipe(tap(() => this.spendLocally(cost))),
    );
  }

  buyAsset(businessId: string, assetCatalogueId: string, price: number): Observable<BusinessDto> {
    return this.afterSync(() =>
      this.http
        .post<BusinessDto>(
          `${this.api}/game/businesses/${businessId}/assets/${assetCatalogueId}`,
          null,
        )
        .pipe(tap(() => this.spendLocally(price))),
    );
  }

  /**
   * A spend owned by a feature outside the business flow (e.g. luxury): sync first, run the
   * purchase, deduct the price from the local purse, then refresh the company — its net worth
   * is server-computed.
   */
  spend<T>(price: number, purchase: () => Observable<T>): Observable<T> {
    return this.afterSync(() =>
      purchase().pipe(
        tap(() => {
          this.spendLocally(price);
          this.refreshCompany().subscribe({ error: () => void 0 });
        }),
      ),
    );
  }

  /** Raises a business one level (more income). Syncs first, like every spend. */
  levelUpBusiness(businessId: string, cost: number): Observable<BusinessDto> {
    return this.afterSync(() =>
      this.http
        .post<BusinessDto>(`${this.api}/game/businesses/${businessId}/level-up`, null)
        .pipe(tap(() => this.spendLocally(cost))),
    );
  }

  /** Hires a manager: the business becomes automated and earns while the player is away. */
  automateBusiness(businessId: string, managerCost: number): Observable<BusinessDto> {
    return this.afterSync(() =>
      this.http
        .post<BusinessDto>(`${this.api}/game/businesses/${businessId}/automate`, null)
        .pipe(tap(() => this.spendLocally(managerCost))),
    );
  }

  /**
   * Refund is `totalValue * (1 - fee)`: 10% normally, 25% in an emergency. `emergency`
   * is a REQUIRED query parameter — omitting it is a framework 400 with an empty body.
   */
  closeBusiness(businessId: string, emergency: boolean): Observable<void> {
    return this.afterSync(() =>
      this.http.delete<void>(`${this.api}/game/businesses/${businessId}?emergency=${emergency}`),
    );
  }

  // -- Prestige -------------------------------------------------------------

  /**
   * A purchase: the price comes out of cash, businesses are kept, the multiplier rises.
   * The response IS the new truth — we adopt it wholesale rather than refetching.
   */
  prestige(): Observable<CompanyDto> {
    return this.afterSync(() =>
      this.http
        .post<CompanyDto>(`${this.api}/game/prestige`, null)
        .pipe(tap((c) => this.applyCompany(c))),
    );
  }

  // -- Bank (contract §6d) --------------------------------------------------

  /**
   * Offers, the active loan and history. The `cash` it carries is the server's last
   * recorded figure — stale against the ticker, like GET /game/company — so it is NOT
   * adopted here; only take/repay (which sync first) adopt it.
   */
  loadBank(): Observable<BankDto> {
    return this.http.get<BankDto>(`${this.api}/game/bank`).pipe(tap((b) => this._bank.set(b)));
  }

  /** Borrows: cash rises by the amount. Syncs first, then adopts the server's cash. */
  takeLoan(offerId: string): Observable<BankDto> {
    return this.afterSync(() =>
      this.http
        .post<BankDto>(`${this.api}/game/bank/loans/${encodeURIComponent(offerId)}`, null)
        .pipe(tap((b) => this.adoptBank(b))),
    );
  }

  /** Pays `outstanding` at once from cash. Syncs first, then adopts the server's cash. */
  repayLoan(): Observable<BankDto> {
    return this.afterSync(() =>
      this.http
        .post<BankDto>(`${this.api}/game/bank/repay`, null)
        .pipe(tap((b) => this.adoptBank(b))),
    );
  }

  /** A take/repay answer is fresh (we synced just before): its cash is the truth, net worth moved. */
  private adoptBank(b: BankDto): void {
    this._bank.set(b);
    this._cash.set(b.cash);
    this.refreshCompany().subscribe({ error: () => void 0 });
  }

  /** After an installment, keep the bank page (if it was ever opened) in step. */
  private reloadBankIfLoaded(): void {
    if (this._bank()) this.loadBank().subscribe({ error: () => void 0 });
  }

  // -- Leaderboard ----------------------------------------------------------

  /** Note the trailing slash — the route is registered as `/api/leaderboard/`. */
  getLeaderboard(take = 50): Observable<LeaderboardEntryDto[]> {
    const clamped = Math.min(100, Math.max(1, Math.trunc(take)));
    return this.http.get<LeaderboardEntryDto[]>(`${this.api}/leaderboard/?take=${clamped}`);
  }

  // -- Loop control ---------------------------------------------------------

  private applyCompany(c: CompanyDto): void {
    this._company.set(c);
    this._cash.set(c.cash);
    this.startLoops();
  }

  /** Keeps the simulated purse in step with a server-side deduction. */
  private spendLocally(amount: number): void {
    if (amount > 0) this._cash.update((c) => Math.max(0, c - amount));
  }

  startLoops(): void {
    this.startTicker();
    this.startSync();
    this.hookUnload();
  }

  /**
   * Credits the time that ACTUALLY passed since the previous tick, not a fixed 1/20 s:
   * browsers throttle timers (background tabs, busy main thread), and a fixed step
   * silently under-counted income — which the server then accepted, since it only
   * clamps figures that are too high.
   */
  private startTicker(): void {
    this.stopTicker();
    this.lastTickAt = performance.now();
    this.tickHandle = setInterval(() => {
      const now = performance.now();
      const seconds = (now - this.lastTickAt) / 1000;
      this.lastTickAt = now;
      const earned = this.cashRate() * seconds;
      if (earned > 0) this._cash.update((c) => c + earned);
      const wall = Date.now();
      if (wall - this._now() >= 1000) this._now.set(wall);
    }, TICK_MS);
  }

  private startSync(): void {
    this.stopSync();
    this.syncHandle = setInterval(() => {
      if (!this._company()) return;
      this.syncNow().subscribe({ error: () => void 0 });
    }, SYNC_MS);
  }

  private stopTicker(): void {
    if (this.tickHandle !== null) {
      clearInterval(this.tickHandle);
      this.tickHandle = null;
    }
  }

  private stopSync(): void {
    if (this.syncHandle !== null) {
      clearInterval(this.syncHandle);
      this.syncHandle = null;
    }
  }

  /** Stops the loops and flushes one last figure. Used on logout. */
  stopAll(): void {
    this.stopTicker();
    this.stopSync();
    this.flush();
  }

  /** Clears game state without touching the session. */
  reset(): void {
    this.stopTicker();
    this.stopSync();
    this._company.set(null);
    this._cash.set(0);
    this._catalogue.set([]);
    this._offlineEarnings.set(null);
    this._achievementToasts.set([]);
    this._bank.set(null);
    this.dismissBankToast();
    this.dismissSyncWarning();
    this._loaded.set(false);
  }

  // -- Leaving the page -----------------------------------------------------

  /**
   * Hidden tab = the player is away. Save once and STOP the loops: a background sync
   * would report a stale figure and move the server's `lastSyncAt` forward, erasing the
   * window `/game/state` is meant to credit. Back on screen, `/game/state` credits the
   * time away (capped, automated income only) and raises the welcome-back dialog —
   * exactly as on a fresh app start. Before this, a tab left open on a locked phone
   * earned nothing at all while away.
   */
  private hookUnload(): void {
    if (this.unloadHooked) return;
    this.unloadHooked = true;
    // `visibilitychange` is the reliable hook — `beforeunload` never fires on mobile.
    document.addEventListener('visibilitychange', () => {
      if (document.visibilityState === 'hidden') {
        this.flush();
        this.stopTicker();
        this.stopSync();
      } else {
        this.resume();
      }
    });
    window.addEventListener('pagehide', () => this.flush());
  }

  private resume(): void {
    if (!this._company() || this.resuming) return;
    this.resuming = true;
    this.bootstrap()
      .pipe(finalize(() => (this.resuming = false)))
      .subscribe({ error: () => this.startLoops() });
  }

  /**
   * Last-gasp sync as the tab goes away.
   *
   * `navigator.sendBeacon` CANNOT carry an Authorization header, and /sync-beacon sits
   * behind RequireAuthorization — a beacon is therefore always a 401 (contract mismatch
   * M1). `fetch` with `keepalive` survives unload the same way and can authenticate, so
   * we use that against the regular /sync endpoint instead.
   */
  private flush(): void {
    const token = this.auth.token();
    if (!token || !this._company()) return;

    void fetch(`${this.api}/game/sync`, {
      method: 'POST',
      keepalive: true,
      headers: {
        'Content-Type': 'application/json',
        Authorization: `Bearer ${token}`,
      },
      body: JSON.stringify({ cash: Math.floor(this._cash()) }),
    }).catch(() => void 0);
  }
}

/** Worth retrying: no answer, a timeout, or the proxy/server not ready yet. */
function isTransient(err: unknown): boolean {
  if (err instanceof TimeoutError) return true;
  return err instanceof HttpErrorResponse && [0, 502, 503, 504].includes(err.status);
}
