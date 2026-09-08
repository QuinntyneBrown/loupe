#requires -Version 7
<#
.SYNOPSIS
  Stops everything docs/demo/harness/setup.ps1 started: the demo containers,
  the network, the host-side demo identity provider, and the Angular dev
  server. Leaves the repository working tree untouched.
#>
param([string]$ScratchDir = (Join-Path $env:TEMP 'loupe-demo-harness'))

Write-Host "Stopping demo containers..." -ForegroundColor Cyan
docker rm -f loupe-demo-api loupe-demo-worker loupe-demo-postgres 2>$null | Out-Null
docker network rm loupe-demo-net 2>$null | Out-Null

Write-Host "Stopping the demo identity provider (port 5444) and Angular dev server (port 4200)..." -ForegroundColor Cyan
foreach ($port in 5444, 4200) {
  $conn = Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue | Select-Object -First 1
  if ($conn) { Stop-Process -Id $conn.OwningProcess -Force -ErrorAction SilentlyContinue }
}

Write-Host "Removing scratch certs/media at $ScratchDir ..." -ForegroundColor Cyan
Remove-Item -Recurse -Force $ScratchDir -ErrorAction SilentlyContinue

Write-Host "Done. The repository working tree was not touched by teardown." -ForegroundColor Green
