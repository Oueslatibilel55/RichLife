# RichLife — backend

.NET 10 API for the RichLife idle/tycoon game: Clean Architecture + DDD, ASP.NET Core
Minimal APIs, EF Core on PostgreSQL, orchestrated in dev by .NET Aspire.

- Coding rules, layers and domain rules: [`CLAUDE.md`](CLAUDE.md)
- Every route and payload: [`../docs/api-contract.md`](../docs/api-contract.md) (source of truth)
- Feature specs: [`../docs/features/`](../docs/features/README.md)

## Requirements

- .NET 10 SDK
- Docker Desktop (Postgres runs in a container)
- `dotnet-ef` for migrations: `dotnet tool install --global dotnet-ef`

## Run it

```bash
# Everything — Postgres, pgAdmin and the API — via Aspire
dotnet run --project src/RichLife.AppHost

# Or the API alone, against the docker-compose Postgres
docker compose up postgres -d
dotnet run --project src/RichLife.Api
```

Migrations are applied by hand in dev (`Database:MigrateOnStartup` is off locally, on in
production) — see below.

## URLs (dev) — all fixed

| Service | Address |
|---|---|
| API | `http://localhost:5187` |
| Scalar (OpenAPI UI, dev only) | `http://localhost:5187/scalar` |
| Aspire dashboard | `http://localhost:18888` |
| pgAdmin | `http://localhost:5050` |
| Postgres (Aspire / docker-compose) | `62749` / `5432` |

## Tests

```bash
dotnet test --solution RichLife.slnx   # the --solution flag is required
```

## Migrations

Run from this folder. Stop the AppHost before building (it locks the API's DLLs), then start
it again so Postgres is reachable for the update.

```bash
./scripts/Add-Migration.ps1 -Name YourMigrationName   # review the generated file first
./scripts/Update-Database.ps1
./scripts/Drop-Database.ps1                           # asks for confirmation
```

## Deployment

Each push to `main` redeploys the API on Render (Docker, `src/RichLife.Api/Dockerfile`,
root dir `RichLife`) against a Neon Postgres; pending migrations are applied on startup.
Details and environment variables: root [`../CLAUDE.md`](../CLAUDE.md) → *Deployed*.

```bash
docker build -f src/RichLife.Api/Dockerfile -t richlife-api .   # same image locally
```
