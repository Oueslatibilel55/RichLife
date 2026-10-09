import { HttpErrorResponse } from '@angular/common/http';
import { hasKey, t } from '../i18n/i18n';
import { prestigeName } from '../game/prestige';

/**
 * Turns a backend failure into something displayable, in the current language.
 *
 * The API has no ProblemDetails envelope (docs/api-contract.md §1): a business
 * failure is a 400 whose body is a bare JSON string, which HttpClient parses into
 * a plain string. 401, 429 and framework-level 400s have EMPTY bodies, so
 * `err.error` is useless for those and we supply the wording instead.
 * `fallback` is a translation key.
 */
export function toErrorMessage(err: unknown, fallback = 'error.generic'): string {
  if (!(err instanceof HttpErrorResponse)) return t(fallback);

  switch (err.status) {
    case 0:
      return t('error.unreachable');
    case 401:
      return t('error.sessionExpired');
    case 429:
      return t('error.rateLimited');
    case 500:
      return t('error.server');
    default: {
      // 400 / 404 carry a bare JSON string; an empty body means a binding failure.
      const body = err.error;
      if (typeof body === 'string' && body.trim().length > 0) return translateServerMessage(body);
      if (err.status === 400) return t('error.rejected');
      return t(fallback);
    }
  }
}

/** Server messages are English (dict/server.ts); unknown ones are shown as sent. */
export function translateServerMessage(message: string): string {
  if (hasKey(message)) return t(message);
  let m = /^Requires prestige (\w+)\.$/.exec(message);
  if (m) return t('server.requiresPrestige', { level: prestigeName(m[1]!) });
  m = /^Need (\d+) assets to unlock this\.$/.exec(message);
  if (m) return t('server.needAssets', { n: m[1]! });
  m = /^Prestige costs (\S+) in cash\.$/.exec(message);
  if (m) return t('server.prestigeCosts', { price: m[1]! });
  return message;
}
