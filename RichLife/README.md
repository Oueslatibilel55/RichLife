# Rich Life _ Backend

## Requirements
- .NET 10 SDK
- Docker Desktop

## Quick start (dev)

\\\ash
# 1. Start PostgreSQL
docker compose up postgres -d

# 2. Run API (with Aspire dashboard)
dotnet run --project src/RichLife.AppHost

# 3. First migration
./scripts/Add-Migration.ps1 -Name InitialCreate
./scripts/Update-Database.ps1
\\\

## URLs (dev)
| Service           | URL                            |
|-------------------|-------------------------------|
| API               | https://localhost:7xxx         |
| Scalar (OpenAPI)  | https://localhost:7xxx/scalar  |
| Aspire Dashboard  | http://localhost:18888         |
| pgAdmin           | http://localhost:5050          |

## Useful commands

\\\ash
# Add a migration
./scripts/Add-Migration.ps1 -Name YourMigrationName

# Apply migrations
./scripts/Update-Database.ps1

# Full docker stack
docker compose up -d
docker compose down -v   # removes volumes too

# Build production image
docker build -f src/RichLife.Api/Dockerfile -t richlife-api .
\\\
"@

# -- Final summary -------------------------------------------------------------
Title "Done!"
Write-Host @"

  Solution created at: C:\Users\Bilel.oueslati\Desktop\BackendRichLife\RichLife

  Next steps:
    1.  cd C:\Users\Bilel.oueslati\Desktop\BackendRichLife\RichLife
    2.  docker compose up postgres -d
    3.  dotnet run --project src/RichLife.AppHost
        _ Aspire Dashboard : http://localhost:18888
        _ API              : https://localhost:7xxx/scalar
    4.  ./scripts/Add-Migration.ps1 -Name InitialCreate
    5.  ./scripts/Update-Database.ps1

