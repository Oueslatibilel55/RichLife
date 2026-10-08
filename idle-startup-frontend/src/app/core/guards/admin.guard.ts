import { inject } from '@angular/core';
import { CanActivateChildFn, CanActivateFn, CanMatchFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';

// Admins are staff, not players: two separate apps behind one login.
// UX only — the API refuses admin tokens on /api/game/* and player tokens on /api/admin/* (403).

/** Admin area: a non-admin never matches it (nor downloads its chunk). */
export const adminMatchGuard: CanMatchFn = (_route, segments) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  if (!auth.isAuthenticated()) {
    return router.createUrlTree(['/login'], { queryParams: { returnUrl: '/' + segments.map((s) => s.path).join('/') } });
  }
  return auth.isAdmin() ? true : router.createUrlTree(['/dashboard']);
};

/** Re-checked on every admin child navigation (the role can be lost on a token refresh). */
export const adminChildGuard: CanActivateChildFn = () =>
  inject(AuthService).isAdmin() ? true : inject(Router).createUrlTree(['/dashboard']);

/** The game: an admin is sent to the admin panel instead — no cash, no businesses. */
export const playerGuard: CanActivateFn = () =>
  inject(AuthService).isAdmin() ? inject(Router).createUrlTree(['/admin']) : true;
