#!/usr/bin/env pwsh
# Run from solution root.
Write-Host "WARNING: This will drop the richlife database. Continue? (y/N): " -NoNewline -ForegroundColor Red

if ((Read-Host) -ne 'y') {
    Write-Host "Cancelled." -ForegroundColor Yellow
    exit 0
}

dotnet ef database drop --force `
    --project src/RichLife.Infrastructure `
    --startup-project src/RichLife.Api

if ($LASTEXITCODE -ne 0) {
    Write-Host "Drop failed (exit $LASTEXITCODE)." -ForegroundColor Red
    exit $LASTEXITCODE
}

Write-Host "Database dropped." -ForegroundColor Green
