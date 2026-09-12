#requires -Version 7
<#
.SYNOPSIS
  Stops everything docs/demo/harness/setup.ps1 started for one stack prefix:
  the demo containers, the network and the Angular dev server. Leaves the
  repository working tree untouched and never touches another prefix's stack.
#>
param(
  [string]$Prefix = 'loupe-demo',
  [int]$AppPort = 4200,
  [string]$ScratchDir = (Join-Path $env:TEMP "$Prefix-harness")
)

Write-Host "Stopping $Prefix containers..." -ForegroundColor Cyan
docker rm -f "$Prefix-api" "$Prefix-worker" "$Prefix-postgres" 2>$null | Out-Null
docker network rm "$Prefix-net" 2>$null | Out-Null

Write-Host "Stopping the Angular dev server (port $AppPort)..." -ForegroundColor Cyan
$conn = Get-NetTCPConnection -LocalPort $AppPort -State Listen -ErrorAction SilentlyContinue | Select-Object -First 1
if ($conn) { Stop-Process -Id $conn.OwningProcess -Force -ErrorAction SilentlyContinue }

Write-Host "Removing scratch certs/media at $ScratchDir ..." -ForegroundColor Cyan
Remove-Item -Recurse -Force $ScratchDir -ErrorAction SilentlyContinue

Write-Host "Done. The repository working tree was not touched by teardown." -ForegroundColor Green
