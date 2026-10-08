import { HttpErrorResponse } from '@angular/common/http';

/**
 * Turns a backend failure into something displayable.
 *
 * The API has no ProblemDetails envelope (docs/api-contract.md §1): a business
 * failure is a 400 whose body is a bare JSON string, which HttpClient parses into
 * a plain string. 401, 429 and framework-level 400s have EMPTY bodies, so
 * `err.error` is useless for those and we supply the wording instead.
 */
export function toErrorMessage(err: unknown, fallback = 'Something went wrong.'): string {
  if (!(err instanceof HttpErrorResponse)) return fallback;

  switch (err.status) {
    case 0:
      return 'Cannot reach the server. Is the API running on port 5187?';
    case 401:
      return 'Your session has expired. Please sign in again.';
    case 429:
      return 'Slow down — too many requests. Try again in a few seconds.';
    case 500:
      return 'The server hit an unexpected error.';
    default: {
      // 400 / 404 carry a bare JSON string; an empty body means a binding failure.
      const body = err.error;
      if (typeof body === 'string' && body.trim().length > 0) return body;
      if (err.status === 400) return 'The server rejected that request.';
      return fallback;
    }
  }
}
