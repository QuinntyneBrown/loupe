param([string]$Filter)
$ErrorActionPreference = 'Stop'
Push-Location (Split-Path $PSScriptRoot -Parent)
try {
    & docker build -f backend/Dockerfile.acceptance -t loupe-acceptance .
    if ($LASTEXITCODE -ne 0) { throw 'Acceptance image build failed.' }
    $dockerArguments = @('run', '--rm', '--cpus', '4', '--memory', '8g',
        '--mount', 'type=bind,source=/var/run/docker.sock,target=/var/run/docker.sock',
        '-e', 'TESTCONTAINERS_HOST_OVERRIDE=host.docker.internal',
        'loupe-acceptance', 'dotnet', 'test', 'backend/Loupe.slnx', '--no-restore')
    if ($Filter) { $dockerArguments += @('--filter', $Filter) }
    & docker @dockerArguments
    if ($LASTEXITCODE -ne 0) { throw 'API acceptance tests failed.' }
}
finally { Pop-Location }
