#!/usr/bin/env pwsh
# Run from solution root.
param([Parameter(Mandatory)][string]$Name)

$ErrorActionPreference = 'Stop'

dotnet ef migrations add $Name `
    --project src/RichLife.Infrastructure `
    --startup-project src/RichLife.Api `
    --output-dir Migrations

if ($LASTEXITCODE -ne 0) {
    Write-Host "Migration '$Name' was NOT created (exit $LASTEXITCODE)." -ForegroundColor Red
    exit $LASTEXITCODE
}

Write-Host "Migration '$Name' created." -ForegroundColor Green
Write-Host "Review it before applying - check defaults for new non-nullable columns and any" -ForegroundColor Yellow
Write-Host "new unique index against existing rows. Then run ./scripts/Update-Database.ps1" -ForegroundColor Yellow
