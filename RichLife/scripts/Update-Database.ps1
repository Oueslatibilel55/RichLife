#!/usr/bin/env pwsh
# Run from solution root.
$ErrorActionPreference = 'Stop'

dotnet ef database update `
    --project src/RichLife.Infrastructure `
    --startup-project src/RichLife.Api

if ($LASTEXITCODE -ne 0) {
    Write-Host "Migration failed (exit $LASTEXITCODE). The database was NOT updated." -ForegroundColor Red
    Write-Host "Postgres must be reachable first: run 'dotnet run --project src/RichLife.AppHost'." -ForegroundColor Yellow
    exit $LASTEXITCODE
}

Write-Host "Database updated." -ForegroundColor Green
