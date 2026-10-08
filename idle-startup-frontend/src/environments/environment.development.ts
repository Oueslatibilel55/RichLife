/**
 * Local development — same-origin `/api`, forwarded by `ng serve` to the API on port 5187
 * (see proxy.conf.json). Same-origin means a tunnel on port 4200 alone is enough to open
 * the app from a phone or another PC.
 */
export const environment = {
  production: false,
  apiUrl: '/api',
};
