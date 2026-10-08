# RichLife — Backend

Idle/tycoon game backend. Players run a `Company`, open businesses, buy assets, accumulate
passive income (online and offline), and **prestige** through seven levels to unlock better
businesses and a permanent income multiplier.

## Tech Stack

| Concern | Choice |
|---|---|
| Runtime | .NET 10 / C# 14 |
| Solution | `RichLife.slnx` (XML solution format) |
| API | ASP.NET Core Minimal APIs |
| Orchestration | .NET Aspire 9.5.2 (`RichLife.AppHost` + `RichLife.ServiceDefaults`) |
| Database | PostgreSQL 17 via Npgsql EF Core 10 |
| Auth | JWT Bearer + BCrypt.Net-Next, refresh tokens |
| API docs | Built-in OpenAPI (`AddOpenApi`) + Scalar UI at `/scalar` (dev only) |
| Caching | `IMemoryCache` |
| Observability | OpenTelemetry via `ServiceDefaults` (OTLP → Aspire dashboard) |
| Testing | xUnit v3 (`xunit.v3.mtp-v2`), Microsoft.Testing.Platform runner |
| Frontend | Angular at `http://localhost:4200` (CORS policy `angular`) |

**Team size: solo.** Optimize for momentum. Skip ceremony that only pays off across a team
(formal ADRs, PR templates, review gates). Keep the architecture rules below anyway — they
are what makes the domain testable.

## Architecture — Clean Architecture + DDD tactical patterns

Dependencies point **inward only**. Never add a reference that reverses this.

```
src/
  RichLife.Domain/          # No project references. No EF, no ASP.NET, no DI.
    Common/                 #   AggregateRoot, BaseEntity, IDomainEvent, Result
    Entities/               #   Company (aggregate root), Business, Asset, BusinessAsset,
                            #   LuxuryAsset, Player
    Catalogue/              #   BusinessCatalogueEntry (aggregate) + AssetCatalogueEntry —
                            #   game content, stored in the database, edited by admins
    Enums/                  #   PrestigeLevel (1-7), BusinessSector
    Events/                 #   BusinessOpenedEvent, PrestigeTriggeredEvent
    GameConstants.cs        #   Tuning values (income tiers, offline cap, fee rates)

  RichLife.Application/     # -> Domain
    Services/               #   AuthService, CompanyService, BusinessService,
                            #   LeaderboardService, CatalogueAdminService, AdminService
    Interfaces/             #   ICompanyRepository, IPlayerRepository, ILeaderboardRepository,
                            #   ICatalogueRepository, IManagerNameRepository,
                            #   IAdminReadRepository, IUnitOfWork, IDomainEventDispatcher,
                            #   IDomainEventHandler<T>
    Mapping/                #   CompanyMapper, CatalogueMapper — entity -> DTO translation
    DTOs/                   #   Request/response records
    Extensions/             #   AddApplication()

  RichLife.Infrastructure/  # -> Application -> Domain
    Persistence/            #   GameDbContext, Configurations/, UnitOfWork,
                            #   CatalogueCacheInvalidator
    Repositories/           #   CompanyRepository, PlayerRepository, LeaderboardRepository,
                            #   CatalogueRepository (cached), ManagerNameRepository,
                            #   AdminReadRepository (admin stats / player list projections)
    Events/                 #   DomainEventDispatcher
    Migrations/             #   EF Core migrations
    Extensions/             #   AddInfrastructure(IConfiguration)

  RichLife.Api/             # -> Application, Infrastructure, ServiceDefaults
    Endpoints/              #   AuthEndpoints, GameEndpoints, BusinessEndpoints,
                            #   LeaderboardEndpoints, AdminCatalogueEndpoints, AdminEndpoints,
                            #   RateLimitPolicies, AuthPolicies, ClaimsPrincipalExtensions
    Program.cs

  RichLife.AppHost/         # Aspire orchestration (Postgres + api)
  RichLife.ServiceDefaults/ # OpenTelemetry, health checks, resilience, service discovery

tests/
  RichLife.Tests/
    Domain/                 #   Pure domain unit tests (no DB, no host)
```

### Layer rules

- **Domain** owns the rules. `Company` is the aggregate root: it guards cash, prestige,
  and its business/asset collections. All mutation goes through methods on the aggregate
  (`AddCash`, `DeductCash`, `OpenBusiness`, `Prestige`, `ApplyOfflineProgress`). Collections
  are exposed as `IReadOnlyList<T>` over private backing lists — never as settable properties.
- **Application** orchestrates: load an aggregate via a repository, call domain methods,
  persist via `IUnitOfWork.CommitAsync`, map to a DTO. It contains no business rules.
- **Infrastructure** is a plugin. It implements Application interfaces. Nothing references it
  except `Api` (for DI registration).
- **Api** endpoints are thin: extract `playerId` from claims, call a service, translate
  `Result` to an HTTP response. No logic.

### Domain patterns in use

- **`Result` / `Result<T>`** (`Domain/Common/Result.cs`) — all fallible operations return a
  `Result`. Do **not** throw for expected business failures (insufficient funds, missing
  business, prestige threshold not met). Throw only for programmer errors and invalid
  arguments (`ArgumentException.ThrowIfNullOrWhiteSpace`).
- **Private constructors + static factories** — `Company.Create(playerId, name)`. EF uses the
  private parameterless constructor.
- **Private setters everywhere** on entities.
- **Domain events** — raised inside the aggregate via `RaiseDomainEvent`, collected on
  `AggregateRoot.DomainEvents`. `UnitOfWork.CommitAsync` saves, clears them, then dispatches
  to every registered `IDomainEventHandler<TEvent>`. Services never dispatch by hand, so no
  command path can forget to publish. To react to an event, register a handler in DI:
  `services.AddScoped<IDomainEventHandler<BusinessOpenedEvent>, YourHandler>()`.
- **`TimeProvider` is injected**, never `DateTime.UtcNow` in a service. Accrual and the sync
  ceiling depend on the clock, and tests pin it to a fixed instant.
- **Repository + Unit of Work** — deliberate here (aggregate-oriented persistence). This is
  *not* the "unnecessary EF wrapper" antipattern; keep it.
- **Catalogue is content, in the database** — businesses and their assets live in
  `catalogue_businesses` / `catalogue_assets` and are edited over `/api/admin/catalogue`,
  never in code. `BusinessCatalogueEntry` is its own aggregate, keyed by an immutable slug
  (`"food-cart"`); a `Business` refers to it by id only. The service loads the entry and
  hands it to the aggregate — `company.OpenBusiness(entry)`, `company.BuyAsset(id, entry,
  assetId)` — so the rules stay in the domain and the domain never touches the database.
  Opening and buying **copy** the entry's numbers, so an admin edit changes future
  openings only. Retire with `IsActive = false`; there is no delete (FK `RESTRICT`).
  Rules — `GameConstants`, the prestige thresholds — deliberately stay in code.

### Persistence rules

Non-obvious things about how this aggregate maps to EF. Breaking one of these produces a
runtime failure that no domain test can catch.

- **Guid keys are assigned by the domain, never by EF.** `Business.Create`,
  `Company.Create` and friends stamp `Id = Guid.NewGuid()`. EF's default for a `Guid` key
  is `ValueGeneratedOnAdd`, and under that default EF decides whether an entity reached
  through a navigation is new by asking *is the key still `Guid.Empty`?* Ours never is, so
  a freshly created child was tracked as `Modified` instead of `Added`: `SaveChanges`
  issued an `UPDATE` against a row that did not exist, and the request failed with
  `DbUpdateConcurrencyException: expected to affect 1 row(s), but actually affected 0` —
  despite there being no concurrency token anywhere in the model.

  `GameDbContext.OnModelCreating` therefore forces every `Guid` primary key to
  `ValueGenerated.Never`. This is a **model-level** loop on purpose: `Asset`,
  `BusinessAsset` and `LuxuryAsset` have no configuration class of their own and had the
  same latent bug. Fixed 2026-10-01; it is model metadata only, so it needs no migration.

- **Reading the generated SQL is the fastest diagnosis.** A wholesale `UPDATE` listing
  *every* column means the entity was marked `Modified` in full; an `UPDATE` listing only
  the columns you actually changed means change tracking worked. Contrasting the two in
  one batch is what located the bug above.

- **The catalogue is cached, untracked, and shared across requests.**
  `CatalogueRepository.GetAllAsync` / `GetByIdAsync` serve one `AsNoTracking` snapshot from
  `IMemoryCache`, so the same entry instances are read by every request — never mutate one
  you got from them. Edits load through `GetForUpdateAsync` (tracked, straight from the
  database). Eviction is automatic: `CatalogueCacheInvalidator`, a scoped
  `SaveChangesInterceptor` on `GameDbContext`, drops the cache after any save that touched a
  catalogue row, so no write path can forget it. A 10-minute TTL backs it up for edits made
  outside the API (hand-written SQL, a second instance).

- **`CompanyRepository.Update` is a deliberate no-op** for an aggregate loaded through the
  repository — it is already tracked, and the change tracker picks up additions and
  removals on its own. It only calls `DbSet.Update` for a genuinely detached graph. Do not
  "fix" it by calling `Update` unconditionally: that marks the whole graph `Modified` and
  reintroduces the failure above for every new child.

### Game rules worth remembering

- Prestige levels 1-7 (`TheHustle` through `GlobalEmpire`); multiplier is
  `1 + 0.18 * PrestigeCount`.
- `Prestige(nowUtc)` is a **purchase** (changed 2026-10-08 — it used to be a full reset):
  it requires `Cash >= GetPrestigeThreshold()` (cash only — business value cannot pay),
  deducts that price, and **keeps the leftover cash and every business**. Passive income
  becomes `BasePassiveIncomePerSecond * PrestigeMultiplier`. Spending the price does not
  touch `AllTimeEarnings`.
- `NetWorth` is cash plus company assets plus each business's `TotalValue`
  (opening cost + its assets).
- **Managers** (2026-10-08): `Company.AutomateBusiness(id, manager, nowUtc)` charges
  `Business.ManagerCost = OpeningCost × GameConstants.ManagerCostMultiplier` (2×) for a
  **4-hour shift** (`GameConstants.ManagerShift`) from the hire, stored as
  `Business.ManagerUntil`; rehire after it ends (refused while one runs). There is no
  `IsAutomated` column any more — "has a manager" is `HasManagerAt(now)`, so anything that
  maps or rates a business needs the current time (`CompanyMapper.ToDto(x, nowUtc)`,
  `OfflineIncomePerSecondAt(now)`). Not refunded on close. `Business.HireManager` is
  unguarded — always go through the aggregate. Each manager gets a random first name from
  the `manager_names` table (130 seeded, content like the catalogue): `BusinessService`
  picks it via `IManagerNameRepository` (avoiding names the company already uses) and
  passes it in; the business stores `ManagerNameId` (FK, `RESTRICT`) plus a copied
  `ManagerName`.
- **Business levels** (2026-10-08): `Company.LevelUpBusiness` pays `Business.NextLevelCost`
  (`OpeningCost × 1.25^(Level − 1)`) and raises `Level` (1..100). `NetIncomePerSecond` is
  `Gross × MultiplierAt(Level) − Salary`, with `+10 %` per level and ×2 at levels 10, 25
  and 50 — so every rate (online, offline, the sync ceiling) includes it. Level spend is
  not part of `TotalValue`. Tuning in `GameConstants`.
- **Luxury** (2026-10-08, `features/008-luxury-collection.md`): `Company.BuyLuxury(item, now)`
  — status only, one of each (unique index `(CompanyId, CatalogueId)`), prestige-gated, paid
  from cash; `NetWorth` includes luxury at its price. Items are content in `luxury_catalogue`
  (76 items in 16 categories, seeded by migrations `LuxuryCollection` + `MoreLuxury`).
  `LuxuryCategory` is stored as an int — append only, never renumber. Each item has an `ImageCredit` that **must stay with the photo** (Wikimedia
  Commons, CC BY / CC BY-SA). Photos live in the frontend at `public/luxury/`.
- **Achievements** (2026-10-08, `features/007-player-profile.md`): defined in code in
  `Domain/Achievements/Achievements.cs` — **codes are persisted, never rename or reuse one**.
  `Company.UnlockAchievements(now)` records newly met ones (owned `company_achievements`,
  PK `CompanyId, Code`) and returns them; it runs on every `/sync` (reported in
  `SyncResultDto.NewAchievements`) and on `GET /api/profile`. Unlocks are permanent;
  `AdminReset` clears them.
- **Admins are staff, not players** (2026-10-08, `features/006-admin-panel.md`):
  `/api/admin/*` is behind `AuthPolicies.Admin`; `/api/game/*` (businesses included) is
  behind `AuthPolicies.Player` (authenticated **and not** Admin), so an admin token gets 403
  in the game. The leaderboard and the admin player stats filter admins out by owner — a
  promoted player keeps their company row. `Company.AdminSetCash` / `AdminReset` set
  **`CashOverridePending`**, which makes the next `Sync` keep the server figure — otherwise
  an online player's 5-second sync would report their old, lower cash and undo the admin
  (the sync clamp only catches figures that are too high). `Player.SetAdmin`; the service
  refuses self-demotion and self-deletion. Dev admin: `admin@admin.com` (password not in
  the repo — ask the owner).
- Offline progress is capped at **4 hours** (`GameConstants.OfflineCap`), counted from
  `LastSyncAt`. Base income is paid for the whole capped window; each business only for
  the part covered by its manager's shift (`Business.ManagedSecondsWithin`). No background
  worker — it is computed once, on return, by `ApplyOfflineProgress`. Both rules live in `Company.ApplyOfflineProgress` — never
  recompute accrual in a service.
- `Company.AccrueOffline(nowUtc)` is the only thing that advances `LastSyncAt` by accrual,
  so `/state` and `/sync` cannot double-credit the same window.
- The client simulates income locally and reports it to `/sync`. `Company.Sync` clamps that
  figure to `MaxPlausibleCash` — never trust a client-supplied balance.
- One business per catalogue entry, per company. Enforced on the aggregate and by a unique
  index on `(CompanyId, CatalogueId)`.
- Closing refunds `TotalValue` minus the fee: 10% graceful, 25% emergency, 40% bankruptcy
  (`GameConstants`). A refund returns capital, so it does not count toward `AllTimeEarnings`.
- Tuning numbers belong in `GameConstants` (rules) or the catalogue tables (content), never
  inlined at a call site.

## Commands

```bash
# Build everything
dotnet build RichLife.slnx

# Run with Aspire (starts Postgres + API)
dotnet run --project src/RichLife.AppHost

# Run the API alone — NOTE: docker-compose Postgres is a SEPARATE, empty database,
# not the one Aspire uses. See "Two Postgres servers run side by side".
docker compose up postgres -d
dotnet run --project src/RichLife.Api

# Tests — MUST pass --solution or --project; bare `dotnet test` reports zero tests
dotnet test --solution RichLife.slnx
dotnet test --project tests/RichLife.Tests/RichLife.Tests.csproj

# Migrations
./scripts/Add-Migration.ps1 -Name YourMigrationName
./scripts/Update-Database.ps1

# Format check
dotnet format --verify-no-changes
```

The three scripts in `scripts/` check `$LASTEXITCODE` and exit non-zero when the
underlying `dotnet ef` command fails. Keep that check in any script you add — they
previously printed "Database updated." after a failed migration.

`global.json` pins the SDK and opts `dotnet test` into Microsoft.Testing.Platform
(`"test": { "runner": "Microsoft.Testing.Platform" }`). Without it, .NET 10 fails with
"Testing with VSTest target is no longer supported".

## Configuration and secrets

**No secrets in `appsettings.json`.** It holds only `Jwt:Issuer`, `Jwt:Audience`, logging levels.

- **Dev** — user secrets on `RichLife.Api` (`UserSecretsId` is set in the csproj):
  ```bash
  dotnet user-secrets set "Jwt:Secret" "<value>" --project src/RichLife.Api
  dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<value>" --project src/RichLife.Api
  ```
- **Prod** — environment variables (`Jwt__Secret`, `ConnectionStrings__DefaultConnection`) or a
  vault. `docker-compose.yml` reads them from `.env`, which is gitignored.
- `Program.cs` fails fast at startup with a clear message if `Jwt:Secret` is missing.

### Which connection string wins

`AddInfrastructure` resolves, in order:

1. `ConnectionStrings:richlife` — what Aspire's `WithReference(richlifeDb)` injects, named
   after the **database resource** in `AppHost/Program.cs`, not after `DefaultConnection`.
2. `ConnectionStrings:DefaultConnection` — docker-compose, `dotnet ef` design-time tooling,
   production.

It throws with an actionable message if neither is set. If you rename the Aspire database
resource, rename it here too, or the API silently falls back to `DefaultConnection` and
talks to a different server than the one Aspire started.

### Aspire dev environment

Every host port below is **pinned**. Nothing here should move between runs — if one does,
that is a bug, not something to work around by chasing the new number.

| What | Port | Pinned by |
|---|---|---|
| API | **5187** | `.WithEndpoint("http", …)` in `AppHost/Program.cs` |
| Postgres | **62749** | `.WithHostPort(62749)` |
| pgAdmin | **5050** | `5050:80` in `docker-compose.yml` (not Aspire) |
| Aspire dashboard | **18888** | `ASPNETCORE_URLS` in the AppHost's `launchSettings.json` |

- The **API is on 5187, unproxied**. `launchSettings.json` also says 5187, but on its own
  that only fixes the port Aspire's proxy listens on — the API behind it is handed a fresh
  random `TargetPort` every run. `AppHost/Program.cs` therefore sets `Port`, `TargetPort`
  **and `IsProxied = false`**, so the API binds 5187 directly. This matters because the
  Angular dev server proxies `/api` to `http://localhost:5187`
  (`idle-startup-frontend/proxy.conf.json`); a moving port breaks the frontend on every
  restart. (The browser itself never calls 5187 — it talks same-origin to 4200, so the
  CORS policy only matters for direct cross-origin calls.)
- Postgres is reachable on **62749**, with the persistent volume `richlife-pgdata`.
- That port belongs to the **DCP proxy**, not the container. `docker ps` always shows the
  container on some random host port — that is normal and is not a misconfiguration. The
  fixed port comes from `.WithHostPort(62749)` in `AppHost/Program.cs`. (The API is the
  exception: it is unproxied, so that indirection does not apply to it.)
- **pgAdmin is not managed by Aspire.** `.WithPgAdmin()` was removed on 2026-10-02: its
  container exited 255 on every run and leaked a dead container each time. pgAdmin now
  comes from the docker-compose `pgadmin` service on **5050**, and the AppHost declares it
  as an external service so the dashboard still shows a clickable, health-checked tile:

  ```csharp
  builder.AddExternalService("pgadmin", "http://localhost:5050")
      .WithHttpHealthCheck("/login");
  ```

  That is a **link with a probe, not a managed resource** — Aspire never starts or stops
  it, so `docker compose stop pgadmin` leaves the tile showing unhealthy, which is correct.
  The URL is hardcoded; if the compose port mapping changes, change this line too.

  Two things about that pgAdmin are hand-made and will not survive a container recreate:
  its registered server (written into pgAdmin's config on an **anonymous** volume, since
  the compose service declares none) and its attachment to Aspire's docker network (added
  with `docker network connect`). Without the network it cannot resolve
  `richlife-postgres-aspire` and the connection fails. Connect it by **container name on
  that network**, never `localhost:62749` — the DCP proxy binds loopback only
  (`127.0.0.1`/`::1`), which is unreachable from inside a container.
- The dashboard is on **http://localhost:18888** (`ASPNETCORE_URLS` in the AppHost's
  `launchSettings.json`), also proxied. It needs a login token, regenerated every run and
  printed by the AppHost as `Login to the dashboard at http://localhost:18888/login?t=...`.
- **The Postgres container is persistent and is never torn down.**
  `.WithLifetime(ContainerLifetime.Persistent)` with
  `.WithContainerName("richlife-postgres-aspire")` means Aspire creates it once and reuses
  it on every later run. It keeps running after the AppHost stops, so `docker exec … psql`
  works without an AppHost session — but **`dotnet ef database update` does not**: port
  62749 is the DCP proxy, which exists only while the AppHost runs (verified 2026-10-02,
  `Failed to connect to 127.0.0.1:62749`). Start the AppHost first, then run
  `dotnet ef database update --no-build` (the running API locks the DLLs a build would
  replace).

  Aspire's default is the opposite: a fresh container per run with a random name suffix,
  removed by DCP **only on a clean shutdown**. A force-kill, a crash, an IDE stop button
  or a reboot leaked a stopped container every time, and a container that dies on startup
  was never cleaned up at all — the list grew without bound (7 orphans had accumulated by
  2026-10-02, the oldest two months old). Do not revert to the default to get a "clean"
  database: the data lives in the `richlife-pgdata` volume, not in the container, so a new
  container changes nothing. To reset the data, remove the volume.

  Because the container is reused, **changes to its configuration do not take effect on an
  existing one** — after editing ports, environment or image in `AppHost/Program.cs`, run
  `docker rm -f richlife-postgres-aspire` once so Aspire recreates it.
- The API holds `RichLife.Infrastructure.dll` while it runs, so a build during an Aspire
  session fails with `MSB3021` file-copy errors. Those are lock errors, not compile errors —
  stop the AppHost and rebuild.

### Two Postgres servers run side by side — only one has the data

This has caused confusion twice. Both are usually running at the same time:

| Container | Started by | Reachable at | Volume | Contents |
|---|---|---|---|---|
| `richlife-postgres-aspire` | Aspire | `localhost:62749` | `richlife-pgdata` (hyphen) | **the real data** — 7 tables, all migrations |
| `richlife-postgres` | `docker-compose.yml` | `localhost:5432` | `richlife_pgdata` (underscore) | **empty** — no migrations ever applied |

The volume names differ by a single character, which is how Compose derives
`<project>_<volume>` from the `pgdata:` entry while Aspire uses the literal name passed to
`.WithDataVolume("richlife-pgdata")`. They are separate databases on separate disks; data
written to one is invisible in the other.

Consequences worth remembering:

- `docker compose up postgres -d && dotnet run --project src/RichLife.Api` — the standalone
  path in **Commands** — starts against the **empty** one. It needs its own
  `Update-Database` run against 5432 and will not show the companies you have been testing
  with under Aspire.
- `richlife-postgres` carries `restart: unless-stopped`, so it comes back on every Docker
  Desktop start whether or not you asked for it. An explicit `docker stop` sticks.
- When something looks like it "lost the data", check which server you are pointed at
  before anything else.

## Coding standards

- File-scoped namespaces for new files. (Some older files use block-scoped — convert
  opportunistically, do not churn files you are not otherwise touching.)
- Primary constructors for services and repositories:
  `public class BusinessService(ICompanyRepository repo, IUnitOfWork uow)`.
- Collection expressions (`[]`), `record` for DTOs, pattern matching / switch expressions.
- `_camelCase` private fields, PascalCase public members, `Async` suffix on async methods.
- Every async method takes `CancellationToken ct = default` and passes it down.
- `decimal` for all money and income values — never `double` or `float`.
- No regions. Comments explain *why*, never *what*.
- Comments and identifiers in English. (A few French comments exist; replace them when you
  touch that code.)

## Testing

- `tests/RichLife.Tests` — xUnit v3 unit tests, **domain-focused**. No database, no host.
- Name tests `Method_Scenario_ExpectedOutcome`.
- Pin the clock. `Company.Create` stamps `LastSyncAt` from the system clock, so fixtures call
  `AccrueOffline(T0)` with a fixed past instant to anchor it before asserting on accrual.
- Every new domain rule (a fee rate, a threshold, an income formula) gets a test. Domain logic
  is pure and cheap to test — there is no excuse for an untested rule.
- Integration tests are deliberately out of scope for now. If that changes, use
  `WebApplicationFactory` + Testcontainers-backed PostgreSQL — never `UseInMemoryDatabase`,
  which silently diverges from Npgsql behavior.
- **Know what the domain tests cannot tell you.** They exercise the aggregate in memory,
  so every persistence-level defect is invisible to them: entity state, generated SQL,
  cascade behaviour, lazy/eager loading. A full green suite said nothing about the
  `Added`-vs-`Modified` bug in **Persistence rules**. When a change touches how an
  aggregate is *stored* rather than how it *behaves*, verify it against a real database
  before calling it done.

## MCP tools (cwm-roslyn-navigator)

Registered globally by the dotnet-claude-kit plugin — no project `.mcp.json` needed.
Prefer these over grep to save tokens:

- `find_symbol` / `get_symbol_detail` — locate a type before editing it
- `find_references` — who uses this, before changing a signature
- `find_implementations` — interface to implementations
- `get_project_graph` — verify no layer violation was introduced
- `get_endpoint_map` — current HTTP surface
- `get_diagnostics` — after changes, before declaring done

## Skills

`modern-csharp`, `clean-architecture`, `ddd`, `minimal-api`, `ef-core`, `testing`,
`error-handling`, `authentication`, `configuration`, `dependency-injection`, `aspire`,
`openapi`, `scalar`, `caching`.

## Workflow

- Plan before any change touching 3+ files or crossing a layer boundary.
- Verify before declaring done: `dotnet build RichLife.slnx` **and**
  `dotnet test --solution RichLife.slnx`.
- Adding a domain rule means adding the test in the same change.
- If an approach starts fighting the layering, stop and re-plan rather than adding a shortcut
  reference.

## Anti-patterns — do not generate

- **Any reference that points outward.** Domain must not reference Application/Infrastructure/EF.
  Application must not reference Infrastructure or ASP.NET Core.
- **Business logic in an endpoint or a service.** Rules belong on the aggregate. If
  `BusinessService` is computing money, that code belongs on `Company` or `Business`.
- **Public setters on entities**, or exposing `List<T>` instead of `IReadOnlyList<T>`.
- **Throwing for expected business failures** — return `Result.Fail(...)`.
- **`double`/`float` for money.** Always `decimal`.
- **`DateTime.Now`** — use `DateTime.UtcNow` (the codebase is UTC throughout), and prefer
  injecting `TimeProvider` for anything that needs to be testable over time.
- **`new HttpClient()`** — use `IHttpClientFactory`.
- **`async void`**, `.Result`, `.Wait()` — always `await`.
- **Dropping `CancellationToken`** partway down a call chain.
- **Returning domain entities from endpoints** — always map to a DTO record.
- **`UseInMemoryDatabase`** in tests.
- **Catching bare `Exception`** — catch specific types.
- **String interpolation in log messages** — use structured templates:
  `logger.LogInformation("Opened {BusinessId}", id)`.
- **Hardcoded game tuning values** at a call site — put them in `GameConstants` or the
  catalogue tables.
- **Letting EF generate a key.** Keys are stamped by the domain factories. Never add
  `ValueGeneratedOnAdd()` to a `Guid` key, and never remove the loop in
  `GameDbContext.OnModelCreating` that sets them all to `ValueGenerated.Never` — see
  **Persistence rules**.

## Open items

Not defects — features whose data model is incomplete. Each needs a game-design decision
before it can be built.

- **The prestige multiplier hits passive income twice.** `Prestige` sets
  `PassiveIncomePerSecond = 3 × PrestigeMultiplier`, and `IncomePerSecond` /
  `OfflineIncomePerSecond` multiply passive by `PrestigeMultiplier` again — 3 × 1.18 × 1.18
  ≈ 4.18/s after one prestige instead of 3.54. Noticed 2026-10-08, not changed: pick one
  place to apply the multiplier (and update the contract's `incomePerSecond` formula).
- **`GameConstants.PassiveIncomeTiers` does not compose with prestige.** The tiers assume a
  passive rate of exactly 3/7/15/35/80/200, but prestige sets the rate to
  `3 * PrestigeMultiplier` (3.54 after one prestige), so "the next tier" is ambiguous.
  `Company.UpgradePassiveIncome` is implemented and tested but deliberately not exposed.
- **The marketplace is half-modelled.** `Business.ListForSale`/`AskingPrice` and
  `Company.ListBusinessForSale` exist; there is no buyer side, no endpoints, no settlement.
- **`Asset` (company-level assets) is persisted but never created.** Table and eager loading
  are in place; no service creates one. `NetWorth` counts it. (`LuxuryAsset` is now used —
  see the luxury rule above.)
- **The leaderboard ranks by `AllTimeEarnings`**, not net worth. Net worth is computed from
  the whole business graph and cannot be ordered in SQL; ranking on it needs a persisted,
  incrementally maintained column.

### Verified how far?

Last checked 2026-10-02, against the Aspire dev database.

**Green.** `dotnet build RichLife.slnx` (0 warnings) and the 72 domain unit tests.
2026-10-08: **73** domain tests green after the prestige-as-purchase change (run with the
API up, so via `--project tests/…` — a full solution build is blocked by the DLL lock).

**Catalogue in the database (2026-10-02).** `20261002161442_MoveCatalogueToDatabase`
applied: 17 businesses and 40 assets seeded, the `RESTRICT` FK validated every existing
business and refuses deleting an owned entry. Over HTTP: the player catalogue is unchanged
in content and order; admin `401`/`403`/`201`/`404`/`400` paths, cache eviction on edit,
retirement, and an owned business keeping its original numbers after a price edit. See
`docs/features/002-catalogue-in-database.md`.

**Catalogue expanded (2026-10-08)** through the admin API (a throwaway `IsAdmin` player,
deleted afterwards): now **52 businesses / 156 asset types**, 7–8 businesses on every
prestige level including `Billionaire` and `GlobalEmpire` (previously empty), and every
sector in use. Content only — no code or migration. A data-only dump of the previous
catalogue was taken first.

**Applied and inspected.** `20260930130802_FixEconomyRulesAndAuditColumns` is in
`__EFMigrationsHistory`. `businesses.CatalogueId` exists as `varchar(60) NOT NULL`, the
unique `IX_businesses_CompanyId_CatalogueId` is in place, and the `CatalogueId` backfill
ran against real rows — three pre-existing businesses were mapped to `food-cart`,
`bike-courier` and `car-wash` with none left on the `''` default.

**Exercised over HTTP.** Register → create company → sync → open a business → buy an
asset → re-open the same business (correctly rejected with `400`). Opening and buying
both persist.

**Prestige against the real database (2026-10-08, under the old full-reset rule).** A real
`TheHustle → SmallBusiness` prestige played through the UI: the company's `businesses` and `BusinessAssets` rows were
deleted, no orphan rows remain anywhere, `Cash` reset, `PassiveIncomePerSecond` became
`3 × 1.18`, and `AllTimeEarnings` survived. So EF's tracking of *removals* from
`Company.Businesses` works — the mechanism whose *additions* were silently wrong until
2026-10-01 (see **Persistence rules**).
