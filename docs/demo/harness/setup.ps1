#requires -Version 7
<#
.SYNOPSIS
  Stands up the local demo stack used to record docs/demo/*.webm from a clean
  environment: PostgreSQL, a throwaway local OIDC identity provider, the real
  Loupe.Api and Loupe.Worker (in Linux containers — see "Why containers" in
  docs/demo/README.md), and the Angular app served same-origin over HTTPS.

  This is demo-only harness tooling. Nothing here is a supported deployment
  path; see backend/README.md for the product's own (not yet delivered)
  Compose increment.

.PARAMETER RepoRoot
  Path to the repository root. Defaults to three levels up from this script.

.PARAMETER ScratchDir
  Where certs, the demo signing key, and demo media are written. Defaults to
  a temp directory; nothing under the repo is touched besides the files this
  script explicitly documents.

.EXAMPLE
  pwsh docs/demo/harness/setup.ps1
#>
param(
  [string]$RepoRoot = (Resolve-Path "$PSScriptRoot/../../..").Path,
  [string]$ScratchDir = (Join-Path $env:TEMP 'loupe-demo-harness')
)

$ErrorActionPreference = 'Stop'
Set-Location $RepoRoot

New-Item -ItemType Directory -Force -Path "$ScratchDir/certs" | Out-Null
New-Item -ItemType Directory -Force -Path "$ScratchDir/demo-media" | Out-Null

Write-Host "== 1. Trust and export the local HTTPS dev certificate ==" -ForegroundColor Cyan
dotnet dev-certs https --trust
dotnet dev-certs https --export-path "$ScratchDir/certs/devcert.pfx" -p 'demo-only' --format Pfx
dotnet dev-certs https --export-path "$ScratchDir/certs/devcert.crt" --format PEM
dotnet dev-certs https --export-path "$ScratchDir/certs/devcert-ng.pem" --format Pem --no-password
# --export-path with --format Pem also writes the matching devcert-ng.key alongside it.

Write-Host "== 2. Start PostgreSQL (demo-only container) ==" -ForegroundColor Cyan
docker rm -f loupe-demo-postgres 2>$null | Out-Null
docker run -d --name loupe-demo-postgres `
  -e POSTGRES_USER=loupe -e POSTGRES_PASSWORD=loupe-demo-only -e POSTGRES_DB=loupe_demo `
  -p 5433:5432 pgvector/pgvector:pg17 | Out-Null
Start-Sleep -Seconds 3
docker exec loupe-demo-postgres pg_isready -U loupe -d loupe_demo

Write-Host "== 3. Apply EF Core migrations ==" -ForegroundColor Cyan
# Infrastructure owns the design-time factory and EF Design dependency.
# No temporary source edits or package changes are needed.
if (-not (Get-Command dotnet-ef -ErrorAction SilentlyContinue)) {
  dotnet tool install --global dotnet-ef --version 10.0.11
  if ($LASTEXITCODE -ne 0) { throw 'Could not install the EF migration tool.' }
}
$previousConnection = $env:ConnectionStrings__Library
try {
  $env:ConnectionStrings__Library = 'Host=localhost;Port=5433;Database=loupe_demo;Username=loupe;Password=loupe-demo-only'
  dotnet ef database update --project "$RepoRoot/backend/src/Loupe.Infrastructure" --startup-project "$RepoRoot/backend/src/Loupe.Infrastructure"
  if ($LASTEXITCODE -ne 0) { throw 'Database migration failed.' }
} finally {
  $env:ConnectionStrings__Library = $previousConnection
}

Write-Host "== 4. Build the demo runtime image (real Api + Worker, Linux libvips) ==" -ForegroundColor Cyan
Copy-Item "$ScratchDir/certs/devcert.crt" "$RepoRoot/backend/.demo-devcert.crt" -Force
try {
  docker build -f docs/demo/harness/Dockerfile.demo-runtime -t loupe-demo-runtime $RepoRoot
} finally {
  Remove-Item "$RepoRoot/backend/.demo-devcert.crt" -ErrorAction SilentlyContinue
}

Write-Host "== 5. Network Postgres with the Api/Worker containers ==" -ForegroundColor Cyan
docker network create loupe-demo-net 2>$null | Out-Null
docker network connect loupe-demo-net loupe-demo-postgres 2>$null | Out-Null

Write-Host "== 6. Run Loupe.Api and Loupe.Worker ==" -ForegroundColor Cyan
docker rm -f loupe-demo-api loupe-demo-worker 2>$null | Out-Null
docker run -d --name loupe-demo-api --network loupe-demo-net -p 5001:5001 `
  -v "${ScratchDir}/certs:/https:ro" -v "${ScratchDir}/demo-media:/data/media" `
  -e ASPNETCORE_URLS='https://+:5001' `
  -e ASPNETCORE_Kestrel__Certificates__Default__Path='/https/devcert.pfx' `
  -e ASPNETCORE_Kestrel__Certificates__Default__Password='demo-only' `
  -e ConnectionStrings__Library='Host=loupe-demo-postgres;Port=5432;Database=loupe_demo;Username=loupe;Password=loupe-demo-only' `
  -e Media__Root='/data/media' `
  -e Identity__Authority='https://host.docker.internal:5444' `
  -e Identity__ClientId='loupe-demo' `
  -e Identity__ClientSecret='demo-only-not-a-real-secret' `
  -e Browser__AllowedOrigins__0='https://localhost:4200' `
  -e Ai__Mode='Live' -e Ai__Endpoint -e Ai__Deployment -e Ai__Model -e Ai__ApiKey -e Imports__Mode='Live' `
  --entrypoint dotnet loupe-demo-runtime /app/api/Loupe.Api.dll | Out-Null
docker run -d --name loupe-demo-worker --network loupe-demo-net `
  -v "${ScratchDir}/demo-media:/data/media" `
  -e ConnectionStrings__Library='Host=loupe-demo-postgres;Port=5432;Database=loupe_demo;Username=loupe;Password=loupe-demo-only' `
  -e Media__Root='/data/media' -e Ai__Mode='Live' -e Ai__Endpoint -e Ai__Deployment -e Ai__Model -e Ai__ApiKey -e Imports__Mode='Live' -e Cleanup__PollInterval='00:00:15' `
  --entrypoint dotnet loupe-demo-runtime /app/worker/Loupe.Worker.dll | Out-Null

Write-Host "== 7. Start the demo identity provider (on the host, port 5444) ==" -ForegroundColor Cyan
Push-Location "$RepoRoot/backend/src/Loupe.DemoIdentityProvider"
dotnet build | Out-Null
$env:ASPNETCORE_URLS = 'https://localhost:5444'
$env:SigningKeyPath = "$ScratchDir/certs/idp-signing-key.pem"
Start-Process -FilePath dotnet -ArgumentList 'run', '--no-build' -WindowStyle Hidden
Remove-Item Env:\ASPNETCORE_URLS, Env:\SigningKeyPath -ErrorAction SilentlyContinue
Pop-Location

Write-Host "== 8. Build the Angular libraries and serve the app over HTTPS, same-origin proxy ==" -ForegroundColor Cyan
Push-Location "$RepoRoot/frontend"
node scripts/mirror-tokens.mjs
npx ng build api; npx ng build components; npx ng build domain
Start-Process -FilePath npx -ArgumentList @(
  'ng', 'serve', 'loupe', '--configuration', 'development', '--ssl',
  '--ssl-cert', "$ScratchDir/certs/devcert-ng.pem", '--ssl-key', "$ScratchDir/certs/devcert-ng.key",
  '--proxy-config', 'proxy.conf.demo.json', '--host', 'localhost', '--port', '4200'
) -WindowStyle Hidden
Pop-Location

Write-Host "`nStack starting. Give the Angular dev server ~10s, then verify:" -ForegroundColor Green
Write-Host "  https://localhost:4200        (the app)"
Write-Host "  https://localhost:5001/api/session   (401 = Api is up)"
Write-Host "  https://localhost:5444/.well-known/openid-configuration  (demo identity provider)"
Write-Host "`nTear down with docs/demo/harness/teardown.ps1" -ForegroundColor Yellow
