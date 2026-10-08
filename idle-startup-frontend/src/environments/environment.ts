/**
 * DEFAULT (production) environment. `environment.development.ts` replaces this file in
 * the `development` build configuration only.
 *
 * A deployed SPA is normally served behind the same origin as its API, so the default
 * is a relative path — point it at an absolute URL if the API lives elsewhere.
 */
export const environment = {
  production: true,
  apiUrl: '/api',
};
