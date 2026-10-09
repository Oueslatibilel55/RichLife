import {
  HttpErrorResponse,
  HttpEvent,
  HttpHandlerFn,
  HttpInterceptorFn,
  HttpRequest,
} from '@angular/common/http';
import { inject } from '@angular/core';
import { Observable, ReplaySubject, catchError, switchMap, take, throwError, timeout } from 'rxjs';
import { AuthService } from '../services/auth.service';

/**
 * Module-level, because the interceptor function runs per request rather than per
 * instance — every in-flight request has to observe the same refresh.
 *
 * Non-null while a refresh is running. Completes with the new access token, or errors
 * if the refresh failed, so queued requests always resolve one way or the other.
 */
let refreshCycle: ReplaySubject<string> | null = null;

/**
 * A refresh that never answers (a phone waking from sleep can leave a request hanging)
 * would keep `refreshCycle` set forever, and every later request would queue behind it —
 * the app stuck on its spinner until the player logged out by hand. Cut it off instead.
 */
const REFRESH_TIMEOUT_MS = 20_000;

function withToken(req: HttpRequest<unknown>, token: string): HttpRequest<unknown> {
  return req.clone({ setHeaders: { Authorization: `Bearer ${token}` } });
}

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const token = auth.token();
  const authed = token ? withToken(req, token) : req;

  return next(authed).pipe(
    catchError((error: HttpErrorResponse) => {
      // /auth/* failures are real credential failures, never an expired access token.
      if (error.status === 401 && !req.url.includes('/auth/')) {
        return handle401(req, next, auth);
      }
      return throwError(() => error);
    }),
  );
};

/**
 * Refreshes once and retries. Concurrent 401s QUEUE on that single refresh instead of
 * logging the user out — with a 5 s sync loop running alongside user actions, two
 * requests failing together is routine, and the previous "second one logs you out"
 * guard dropped healthy sessions.
 */
function handle401(
  req: HttpRequest<unknown>,
  next: HttpHandlerFn,
  auth: AuthService,
): Observable<HttpEvent<unknown>> {
  return (refreshCycle ?? startRefresh(auth)).pipe(
    take(1),
    switchMap((t) => next(withToken(req, t))),
  );
}

/**
 * Runs the refresh on its own subscription, not on the request that hit the 401: if that
 * request is cancelled (navigation, a caller's timeout) the refresh must still finish,
 * or `refreshCycle` would stay set and every later request would wait on it forever.
 */
function startRefresh(auth: AuthService): ReplaySubject<string> {
  const cycle = new ReplaySubject<string>(1);
  refreshCycle = cycle;

  auth
    .refresh()
    .pipe(timeout(REFRESH_TIMEOUT_MS))
    .subscribe({
      next: (res) => {
        refreshCycle = null;
        cycle.next(res.accessToken);
        cycle.complete();
      },
      error: (err: unknown) => {
        refreshCycle = null;
        // Fail the queued requests too rather than leaving them hanging forever.
        cycle.error(err);
        auth.logout('expired');
      },
    });

  return cycle;
}
