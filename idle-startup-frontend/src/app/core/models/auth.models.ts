/** Wire contracts for /api/auth/* — docs/api-contract.md §3. */

export interface AuthResponse {
  accessToken: string;
  refreshToken: string;
  playerId: string;
  username: string;
}

export interface RegisterRequest {
  username: string;
  email: string;
  password: string;
  /** ISO-3166 alpha-2, exactly 2 characters. */
  country: string;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface SessionUser {
  playerId: string;
  username: string;
}
