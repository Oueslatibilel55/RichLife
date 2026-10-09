# RichLife — monorepo root

Idle/tycoon game. Two halves, one HTTP contract between them.

| Folder | What it is |
|---|---|
| `RichLife/` | .NET 10 backend — Clean Architecture + DDD, ASP.NET Core Minimal APIs, PostgreSQL, Aspire. Has its own `CLAUDE.md` with the layer rules, domain rules and coding standards. |
| `idle-startup-frontend/` | Angular 19 standalone frontend — signals, no NgModules. Has its own `CLAUDE.md` with the component/service conventions. |
| `docs/` | Shared documentation. |

Work inside one half follows that half's `CLAUDE.md`. Work that crosses the boundary
follows this file.

## The contract is the source of truth

@docs/api-contract.md

Every route, request/response shape, status code and error message between the two halves
is defined there. When the two sides disagree, the contract wins — fix the code, not the
document. It also carries the open frontend/backend mismatches at the bottom.

**Rule for any API change: contract first, then backend, then frontend.** Update
`docs/api-contract.md`, then make the backend match it, then make the frontend match it.
A backend change that lands without a contract update is incomplete.

## Shared conventions

- **JSON casing** — camelCase on the wire, both directions. Backend records stay
  PascalCase; the serializer converts.
- **Enums** — always serialized as strings (`"SmallBusiness"`), never as numbers. Adding a
  value means updating the contract and the string-keyed maps in
  `dashboard.component.ts`, `layout.component.ts` and `businesses.component.ts`.
- **Dates** — UTC only, ISO-8601 with `Z`. `TimeSpan` serializes as `"hh:mm:ss"`.
- **Money** — `decimal` server-side, JSON number on the wire. Never `double`/`float`.
- **Errors** — no ProblemDetails envelope. A business failure is a `400` whose body is a
  bare JSON string, which Angular surfaces as `err.error`. `401` and `429` have empty
  bodies. The game UI is in English, French and Arabic, but the server keeps answering in
  English: a new or reworded message also needs its fr/ar line in
  `idle-startup-frontend/src/app/core/i18n/dict/server.ts`, or players see it in English.
- **Pagination** — none. Lists are bare JSON arrays. The only limiter is
  `/api/leaderboard?take=` (1..100, default 50).
- **Auth** — `Authorization: Bearer <accessToken>`; access token 1 h, refresh token 7 days
  and rotated on every refresh (store both halves of the response).

## Running it

```bash
# Backend — everything (Postgres + pgAdmin + API) via Aspire
cd RichLife && dotnet run --project src/RichLife.AppHost

# Backend — API alone, against a docker Postgres
cd RichLife && docker compose up postgres -d && dotnet run --project src/RichLife.Api

# Backend tests — the --solution flag is required
cd RichLife && dotnet test --solution RichLife.slnx

# Frontend — dev server on http://localhost:4200
cd idle-startup-frontend && ng serve
```

### Ports — all fixed

| What | Address |
|---|---|
| API | `http://localhost:5187` |
| Frontend | `http://localhost:4200` |
| Postgres (Aspire) | `62749` |
| Postgres (docker-compose) | `5432` |
| pgAdmin | `5050` |
| Aspire dashboard | `http://localhost:18888` |

None of these move between runs, under Aspire or standalone. The API in particular is
pinned in `RichLife/src/RichLife.AppHost/Program.cs` with `IsProxied = false` — without
that, Aspire hands the API a fresh random port on every run while only its proxy stays on
5187. **In dev the frontend calls same-origin `/api`, and `ng serve` proxies it to
`http://localhost:5187` (`idle-startup-frontend/proxy.conf.json`), so the proxy target does
not tolerate a moving port.** If the frontend starts reporting "Cannot reach the server",
check that the API is actually up before assuming the port drifted.

### Deployed (free tier) — since 2026-10-08

Code: `https://github.com/Oueslatibilel55/RichLife` (public — never commit secrets;
`Creds.txt` and `.env` are git-ignored). Each push to `main` redeploys both halves.

| Part | Host | Notes |
|---|---|---|
| Frontend | **Netlify** — `netlify.toml` at the repo root | Builds `idle-startup-frontend`, publishes `dist/idle-startup-frontend/browser`, **proxies `/api/*` to Render** (so same-origin, no CORS) and falls back to `index.html` for SPA routes. |
| API | **Render** web service `https://richlife.onrender.com` | Docker: root dir `RichLife`, Dockerfile `src/RichLife.Api/Dockerfile`, region Frankfurt. Free tier **sleeps after ~15 min idle**; the first request then takes 30–60 s (Netlify's proxy may time out once — retry). |
| Database | **Neon** Postgres 17, `eu-central-1` | Seeded 2026-10-08 with a `pg_dump` of the local Aspire database (catalogue edits made through the admin API are data, not migrations, so a fresh database would lack them). |

Render environment variables (values live only in Render): `ASPNETCORE_ENVIRONMENT=Production`,
`PORT=8080`, `Jwt__Secret`, `ConnectionStrings__DefaultConnection` (Npgsql key=value form,
Neon's **direct** host — not `-pooler` — with `SSL Mode=Require`), and
`Database__MigrateOnStartup=true` (applies pending EF migrations on boot; off locally).
Behind Netlify + Render the API reads the client IP from `X-Forwarded-For`
(`ForwardLimit = 2`) so rate limits stay per visitor.

### Sharing the dev app (phone, colleague)

Because the browser only ever talks to port 4200, forwarding **that one port** is enough —
VS Code *Ports → Forward a Port → 4200* (Dev Tunnels), `cloudflared tunnel --url
http://localhost:4200`, or ngrok. The dev server only answers hostnames listed in
`allowedHosts` (`angular.json` → serve → development): `*.devtunnels.ms`,
`*.trycloudflare.com`, `*.ngrok-free.app`. Add a domain there for any other tunnel. The
API itself is never exposed, and CORS is not involved (same origin).

From a shell, the Microsoft `devtunnel` CLI (installed via `winget install Microsoft.devtunnel`,
signed in with GitHub) does the same as the VS Code Ports panel. A tunnel outlives the
process hosting it (it expires after ~30 days), so after a restart **re-host the existing one**
to keep the same URL instead of creating a new one:

```bash
devtunnel list                             # existing tunnels and their expiry
devtunnel host <tunnel-id>                 # re-host: same URL as before
devtunnel host -p 4200 --allow-anonymous   # creates a NEW tunnel, with a new URL
```

**When testers use the tunnel, run `ng serve --live-reload=false --hmr=false`.** With live
reload on, Vite's client keeps a WebSocket open; a phone that sleeps or a backgrounded tab
drops it, and on return the client force-reloads the page (`location.reload()` after
"server connection lost"). That reload lands right after the welcome-back popup appears and
wipes it — the offline earnings are still credited server-side, but the player never sees
them. With both flags off Angular sets Vite `ws: false`: no socket, no reload (verified: 0
WebSockets). Testers refresh by hand after a code change. A production build never has this.

Anonymous = anyone with the link can register on the dev database. Visitors see a one-time
Microsoft "developer tunnel" warning page first.

Node here is v18.18.0 (`C:\Program Files\nodejs`, not on the agent shell's `PATH`), below
the v18.19 the Angular 19 CLI requires — plain `ng` refuses to start. Until it is upgraded,
run the local CLI on a throwaway Node 20 from the npx cache (system Node untouched):

```bash
cd idle-startup-frontend && export PATH="/c/Program Files/nodejs:$PATH"
npx -y -p node@20 node node_modules/@angular/cli/bin/ng.js serve --port 4200   # or: build
```

Stopping the shell that launched `ng serve` can leave the node process holding port 4200 —
check `Get-NetTCPConnection -LocalPort 4200` before starting another one.
