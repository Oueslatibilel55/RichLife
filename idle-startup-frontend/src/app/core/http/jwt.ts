/**
 * Reads the access token's claims client-side — for showing or hiding UI only. The
 * server checks the signature and the role on every admin call; nothing here is trusted.
 */

/** .NET writes the role under its full claim URI, not a short `role` key (contract §7). */
const ROLE_CLAIMS = ['http://schemas.microsoft.com/ws/2008/06/identity/claims/role', 'role'];

export function jwtPayload(token: string | null): Record<string, unknown> | null {
  const part = token?.split('.')[1];
  if (!part) return null;
  try {
    const base64 = part.replace(/-/g, '+').replace(/_/g, '/').padEnd(Math.ceil(part.length / 4) * 4, '=');
    const bytes = Uint8Array.from(atob(base64), (c) => c.charCodeAt(0));
    return JSON.parse(new TextDecoder().decode(bytes)) as Record<string, unknown>;
  } catch {
    return null;
  }
}

export function tokenRoles(token: string | null): string[] {
  const payload = jwtPayload(token);
  if (!payload) return [];
  return ROLE_CLAIMS.flatMap((key) => {
    const value = payload[key];
    if (typeof value === 'string') return [value];
    return Array.isArray(value) ? value.filter((v): v is string => typeof v === 'string') : [];
  });
}
