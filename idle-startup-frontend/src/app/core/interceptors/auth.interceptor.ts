import {
  HttpErrorResponse,
  HttpEvent,
  HttpHandlerFn,
  HttpInterceptorFn,
  HttpRequest,
} from '@angular/common/http';
import { inject } from '@angular/core';
import { Observable, ReplaySubject, catchError, switchMap, take, throwError } from 'rxjs';
import { AuthService } from '../services/auth.service';

/**
 * Module-level, because the interceptor function runs per request rather than per
 * instance — every in-flight request has to observe the same refresh.
 *
 * Non-null while a refresh is running. Completes with the new access token, or errors
 * if the refresh failed, so queued requests always resolve one way or the other.
 */
let refreshCycle: ReplaySubject<string> | null = null;

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
  const pending = refreshCycle;
  if (pending) {
    return pending.pipe(
      take(1),
      switchMap((t) => next(withToken(req, t))),
    );
  }

  const cycle = new ReplaySubject<string>(1);
  refreshCycle = cycle;

  return auth.refresh().pipe(
    switchMap((res) => {
      refreshCycle = null;
      cycle.next(res.accessToken);
      cycle.complete();
      return next(withToken(req, res.accessToken));
    }),
    catchError((err: unknown) => {
      refreshCycle = null;
      // Fail the queued requests too rather than leaving them hanging forever.
      cycle.error(err);
      auth.logout();
      return throwError(() => err);
    }),
  );
}
