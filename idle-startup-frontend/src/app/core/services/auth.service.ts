import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { Observable, tap, throwError } from 'rxjs';
import { tokenRoles } from '../http/jwt';
import { environment } from '../../../environments/environment';
import {
  AuthResponse,
  LoginRequest,
  RegisterRequest,
  SessionUser,
} from '../models/auth.models';

const TOKEN_KEY = 'rl_token';
const REFRESH_KEY = 'rl_refresh';
const USER_KEY = 'rl_user';
const OFFLINE_FLAG_KEY = 'rl_offline_shown';

/** localStorage throws in private-mode Safari and when site data is blocked. */
function readStorage(key: string): string | null {
  try {
    return localStorage.getItem(key);
  } catch {
    return null;
  }
}

function writeStorage(key: string, value: string): void {
  try {
    localStorage.setItem(key, value);
  } catch {
    /* session simply will not survive a reload */
  }
}

function clearStorage(keys: string[]): void {
  try {
    keys.forEach((k) => localStorage.removeItem(k));
    sessionStorage.removeItem(OFFLINE_FLAG_KEY);
  } catch {
    /* nothing to clean up */
  }
}

function readUser(): SessionUser | null {
  const raw = readStorage(USER_KEY);
  if (!raw) return null;
  try {
    return JSON.parse(raw) as SessionUser;
  } catch {
    return null;
  }
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly api = environment.apiUrl;

  private readonly _token = signal<string | null>(readStorage(TOKEN_KEY));
  private readonly _user = signal<SessionUser | null>(readUser());

  readonly token = this._token.asReadonly();
  readonly currentUser = this._user.asReadonly();
  readonly isAuthenticated = computed(() => this._token() !== null);

  /**
   * From the token's role claim — drives the admin nav and route guard only. The server
   * re-checks the role on every /api/admin call. A promotion shows up after the next
   * login or token refresh, when a new token is issued.
   */
  readonly isAdmin = computed(() => tokenRoles(this._token()).includes('Admin'));

  register(req: RegisterRequest): Observable<AuthResponse> {
    return this.http
      .post<AuthResponse>(`${this.api}/auth/register`, req)
      .pipe(tap((res) => this.saveSession(res)));
  }

  login(req: LoginRequest): Observable<AuthResponse> {
    return this.http
      .post<AuthResponse>(`${this.api}/auth/login`, req)
      .pipe(tap((res) => this.saveSession(res)));
  }

  /**
   * Both tokens in the response are new — the refresh token rotates and the old one
   * stops working immediately, so the whole response must be stored.
   */
  refresh(): Observable<AuthResponse> {
    const refreshToken = readStorage(REFRESH_KEY);
    if (!refreshToken) return throwError(() => new Error('No refresh token'));

    return this.http
      .post<AuthResponse>(`${this.api}/auth/refresh`, { refreshToken })
      .pipe(tap((res) => this.saveSession(res)));
  }

  /** `'expired'` tells the login page to explain why the player was signed out. */
  logout(reason?: 'expired'): void {
    this.clearSession();
    void this.router.navigate(['/login'], reason ? { queryParams: { reason } } : {});
  }

  /** Drops the session without navigating — used by the interceptor mid-flight. */
  clearSession(): void {
    clearStorage([TOKEN_KEY, REFRESH_KEY, USER_KEY]);
    this._token.set(null);
    this._user.set(null);
  }

  private saveSession(res: AuthResponse): void {
    writeStorage(TOKEN_KEY, res.accessToken);
    writeStorage(REFRESH_KEY, res.refreshToken);
    writeStorage(USER_KEY, JSON.stringify({ playerId: res.playerId, username: res.username }));
    this._token.set(res.accessToken);
    this._user.set({ playerId: res.playerId, username: res.username });
  }
}
