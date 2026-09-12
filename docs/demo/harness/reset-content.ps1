#requires -Version 7
<#
.SYNOPSIS
  Empties the library content of one demo stack's PostgreSQL database so a
  recording can be seeded and re-taken from the same clean state. Accounts,
  sessions and the migration history are kept. Only the container named
  <Prefix>-postgres is touched, and only when it was created by setup.ps1
  (it carries the demo-only credentials that script sets).
#>
param([string]$Prefix = 'loupe-demo')

$ErrorActionPreference = 'Stop'
$postgres = "$Prefix-postgres"
$environment = (docker inspect $postgres --format '{{join .Config.Env "\n"}}' 2>$null) -join "`n"
if ($LASTEXITCODE -ne 0 -or -not $environment) { throw "No container named $postgres is running." }
if ($environment -notmatch 'POSTGRES_PASSWORD=loupe-demo-only' -or $environment -notmatch 'POSTGRES_DB=loupe_demo') {
  throw "$postgres was not created by docs/demo/harness/setup.ps1; refusing to reset it."
}

# analysis_dispatch_cursor is a singleton row the worker polls; it is reset, never truncated.
$sql = @'
TRUNCATE photographs, "references", reference_drafts, reference_tags, board_references, boards, photographers,
  photographer_drafts, photographer_tags, background_operations, operation_receipts, journal.deletions CASCADE;
UPDATE analysis_dispatch_cursor SET "OwnerId" = '' WHERE "Id" = 1;
'@
$sql | docker exec -i $postgres psql -U loupe -d loupe_demo -v ON_ERROR_STOP=1
if ($LASTEXITCODE -ne 0) { throw 'Content reset failed.' }
Write-Host "Library content in $postgres emptied; accounts kept." -ForegroundColor Green
