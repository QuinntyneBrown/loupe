#requires -Version 7
<#
.SYNOPSIS
  Stands up the local demo stack used to record docs/demo recordings from a clean
  environment: PostgreSQL, locally provisioned accounts, the real
  Loupe.Api and Loupe.Worker (in Linux containers — see "Why containers" in
  docs/demo/README.md), and the Angular app served same-origin over HTTPS.

  This is demo-only harness tooling. Nothing here is a supported deployment
  path; see backend/README.md for the product's own (not yet delivered)
  Compose increment.

.PARAMETER RepoRoot
  Path to the repository root. Defaults to three levels up from this script.

.PARAMETER Prefix
  Name prefix for every container, network and image this stack owns
  (<Prefix>-postgres, <Prefix>-api, <Prefix>-worker, <Prefix>-net,
  <Prefix>-runtime). Give each concurrently running stack its own prefix so
  one recording never resets another's database.

.PARAMETER DbPort
  Host port published for the demo PostgreSQL container.

.PARAMETER ApiPort
  Host port published for Loupe.Api (HTTPS). The Angular proxy targets it.

.PARAMETER AppPort
  Port for the Angular dev server. https://localhost:<AppPort> is the only
  browser origin the API trusts.

.PARAMETER ScratchDir
  Where certs, the generated proxy configuration, and demo media are written.
  Defaults to a temp directory named after the prefix; nothing under the repo
  is touched besides the files this script explicitly documents.

.PARAMETER Ollama
  Also start a local Ollama container (<Prefix>-ollama) on the stack network,
  pull the bge-m3 embedding model into it (about 1.2 GB on first use, kept in
  the shared loupe-ollama-models volume), and point the Api and Worker at it through
  Embeddings__Endpoint so Find a location's Meaning mode and the search index
  worker run. Without this switch Meaning mode reports search unavailable and
  each location shows no search-index status; keyword search is unaffected.

.EXAMPLE
  pwsh docs/demo/harness/setup.ps1

.EXAMPLE
  pwsh docs/demo/harness/setup.ps1 -Prefix loupe-insp-demo -DbPort 5434 -ApiPort 5011 -AppPort 4210
#>
param(
  [string]$RepoRoot = (Resolve-Path "$PSScriptRoot/../../..").Path,
  [string]$Prefix = 'loupe-demo',
  [int]$DbPort = 5433,
  [int]$ApiPort = 5001,
  [int]$AppPort = 4200,
  [string]$ScratchDir = (Join-Path $env:TEMP "$Prefix-harness"),
  [switch]$Ollama
)

$ErrorActionPreference = 'Stop'
Set-Location $RepoRoot

$postgres = "$Prefix-postgres"; $api = "$Prefix-api"; $worker = "$Prefix-worker"; $network = "$Prefix-net"; $image = "$Prefix-runtime"
$ollamaContainer = "$Prefix-ollama"
$appOrigin = "https://localhost:$AppPort"
$hostConnection = "Host=localhost;Port=$DbPort;Database=loupe_demo;Username=loupe;Password=loupe-demo-only"
$containerConnection = "Host=$postgres;Port=5432;Database=loupe_demo;Username=loupe;Password=loupe-demo-only"

New-Item -ItemType Directory -Force -Path "$ScratchDir/certs" | Out-Null
New-Item -ItemType Directory -Force -Path "$ScratchDir/demo-media" | Out-Null

Write-Host "== 1. Trust and export the local HTTPS dev certificate ==" -ForegroundColor Cyan
dotnet dev-certs https --trust
dotnet dev-certs https --export-path "$ScratchDir/certs/devcert.pfx" -p 'demo-only' --format Pfx
dotnet dev-certs https --export-path "$ScratchDir/certs/devcert.crt" --format PEM
dotnet dev-certs https --export-path "$ScratchDir/certs/devcert-ng.pem" --format Pem --no-password
# --export-path with --format Pem also writes the matching devcert-ng.key alongside it.

Write-Host "== 2. Start PostgreSQL ($postgres, host port $DbPort) ==" -ForegroundColor Cyan
docker rm -f $postgres 2>$null | Out-Null
docker run -d --name $postgres `
  -e POSTGRES_USER=loupe -e POSTGRES_PASSWORD=loupe-demo-only -e POSTGRES_DB=loupe_demo `
  -p "${DbPort}:5432" pgvector/pgvector:pg17 | Out-Null
Start-Sleep -Seconds 3
docker exec $postgres pg_isready -U loupe -d loupe_demo

Write-Host "== 3. Apply EF Core migrations ==" -ForegroundColor Cyan
# Infrastructure owns the design-time factory and EF Design dependency.
# No temporary source edits or package changes are needed.
if (-not (Get-Command dotnet-ef -ErrorAction SilentlyContinue)) {
  dotnet tool install --global dotnet-ef --version 10.0.11
  if ($LASTEXITCODE -ne 0) { throw 'Could not install the EF migration tool.' }
}
$previousConnection = $env:ConnectionStrings__Library
try {
  $env:ConnectionStrings__Library = $hostConnection
  dotnet ef database update --project "$RepoRoot/backend/src/Loupe.Infrastructure" --startup-project "$RepoRoot/backend/src/Loupe.Infrastructure"
  if ($LASTEXITCODE -ne 0) { throw 'Database migration failed.' }
} finally {
  $env:ConnectionStrings__Library = $previousConnection
}

Write-Host "== 4. Build the demo runtime image (${image}: real Api + Worker, Linux libvips) ==" -ForegroundColor Cyan
Copy-Item "$ScratchDir/certs/devcert.crt" "$RepoRoot/backend/.demo-devcert.crt" -Force
try {
  docker build -f docs/demo/harness/Dockerfile.demo-runtime -t $image $RepoRoot
  if ($LASTEXITCODE -ne 0) { throw 'Demo runtime image build failed.' }
} finally {
  Remove-Item "$RepoRoot/backend/.demo-devcert.crt" -ErrorAction SilentlyContinue
}

Write-Host "== 5. Network Postgres with the Api/Worker containers ($network) ==" -ForegroundColor Cyan
docker network create $network 2>$null | Out-Null
docker network connect $network $postgres 2>$null | Out-Null

$env:Jwt__SigningKey = [Convert]::ToBase64String([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
if ($Ollama) {
  Write-Host "== 5b. Start Ollama ($ollamaContainer) and pull bge-m3 for local embeddings ==" -ForegroundColor Cyan
  docker rm -f $ollamaContainer 2>$null | Out-Null
  # Pulled models are immutable downloads, so every stack shares one volume instead of pulling 1.2 GB per prefix.
  # OLLAMA_KEEP_ALIVE keeps bge-m3 resident; the default unloads it after five idle minutes, and the reload adds seconds to the next query.
  docker run -d --name $ollamaContainer --network $network -e OLLAMA_KEEP_ALIVE=24h -v "loupe-ollama-models:/root/.ollama" ollama/ollama | Out-Null
  # The server inside the container takes a moment to listen; pull only once it answers.
  $ready = $false
  foreach ($attempt in 1..30) {
    docker exec $ollamaContainer ollama list *>$null
    if ($LASTEXITCODE -eq 0) { $ready = $true; break }
    Start-Sleep -Seconds 1
  }
  if (-not $ready) { throw 'Ollama did not start.' }
  docker exec $ollamaContainer ollama pull bge-m3
  if ($LASTEXITCODE -ne 0) { throw 'Pulling bge-m3 failed.' }
  $env:Embeddings__Endpoint = "http://${ollamaContainer}:11434"
  $env:Embeddings__Model = 'bge-m3'
}

Write-Host "== 6. Run Loupe.Api ($api, host port $ApiPort) and Loupe.Worker ($worker) ==" -ForegroundColor Cyan
docker rm -f $api $worker 2>$null | Out-Null
docker run -d --name $api --network $network -p "${ApiPort}:5001" `
  -v "${ScratchDir}/certs:/https:ro" -v "${ScratchDir}/demo-media:/data/media" `
  -e ASPNETCORE_URLS='https://+:5001' `
  -e ASPNETCORE_Kestrel__Certificates__Default__Path='/https/devcert.pfx' `
  -e ASPNETCORE_Kestrel__Certificates__Default__Password='demo-only' `
  -e ConnectionStrings__Library=$containerConnection `
  -e Media__Root='/data/media' `
  -e Jwt__Issuer='Loupe' -e Jwt__Audience='Loupe' -e Jwt__SigningKey `
  -e Browser__AllowedOrigins__0=$appOrigin `
  -e Ai__Mode='Live' -e Ai__Endpoint -e Ai__Deployment -e Ai__Model -e Ai__ApiKey -e Imports__Mode='Live' `
  -e Embeddings__Endpoint -e Embeddings__Model `
  --entrypoint dotnet $image /app/api/Loupe.Api.dll | Out-Null
docker run -d --name $worker --network $network `
  -v "${ScratchDir}/demo-media:/data/media" `
  -e ConnectionStrings__Library=$containerConnection `
  -e Media__Root='/data/media' -e Ai__Mode='Live' -e Ai__Endpoint -e Ai__Deployment -e Ai__Model -e Ai__ApiKey -e Imports__Mode='Live' -e Cleanup__PollInterval='00:00:15' `
  -e Embeddings__Endpoint -e Embeddings__Model `
  --entrypoint dotnet $image /app/worker/Loupe.Worker.dll | Out-Null

Remove-Item Env:\Jwt__SigningKey
Write-Host "== 7. Provision local demo accounts ==" -ForegroundColor Cyan
foreach ($email in 'photographer@example.com', 'api-demo@example.com') {
  'local acceptance password' | docker run --rm -i --network $network `
    -e ConnectionStrings__Library=$containerConnection `
    --entrypoint dotnet $image /app/admin/Loupe.Admin.dll create-user $email 'Demo Photographer'
  if ($LASTEXITCODE -ne 0) { throw 'Demo account provisioning failed.' }
}

Write-Host "== 8. Build the Angular libraries and serve the app over HTTPS, same-origin proxy (port $AppPort) ==" -ForegroundColor Cyan
# The proxy configuration is generated per stack so /api reaches this stack's API port.
$proxyConfig = Join-Path $ScratchDir 'proxy.conf.json'
@{ '/api' = @{ target = "https://localhost:$ApiPort"; secure = $false; changeOrigin = $false } } |
  ConvertTo-Json -Depth 3 | Set-Content -Path $proxyConfig -Encoding utf8
Push-Location "$RepoRoot/frontend"
node scripts/mirror-tokens.mjs
npx ng build api; npx ng build components; npx ng build domain
# Launched through cmd.exe so the npm shim resolves the same way as in a terminal;
# its output is kept in the scratch directory for diagnosis.
$serve = "npx ng serve loupe --configuration development --ssl --ssl-cert `"$ScratchDir/certs/devcert-ng.pem`" --ssl-key `"$ScratchDir/certs/devcert-ng.key`" --proxy-config `"$proxyConfig`" --host localhost --port $AppPort"
Start-Process -FilePath 'cmd.exe' -ArgumentList @('/d', '/s', '/c', $serve) -WorkingDirectory "$RepoRoot/frontend" -WindowStyle Hidden `
  -RedirectStandardOutput (Join-Path $ScratchDir 'ng-serve.log') -RedirectStandardError (Join-Path $ScratchDir 'ng-serve.err.log')
Pop-Location

Write-Host "`nStack starting. Give the Angular dev server ~10s, then verify:" -ForegroundColor Green
Write-Host "  $appOrigin        (the app)"
Write-Host "  https://localhost:$ApiPort/api/session   (401 = Api is up)"
Write-Host "`nTear down with docs/demo/harness/teardown.ps1 -Prefix $Prefix -AppPort $AppPort" -ForegroundColor Yellow
