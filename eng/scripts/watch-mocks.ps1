<#
.SYNOPSIS
Opens the newest HTML mock in Chrome, then watches for updates until Ctrl+C.
.EXAMPLE
.\eng\scripts\watch-mocks.ps1
.EXAMPLE
.\eng\scripts\watch-mocks.ps1 -IntervalSeconds 5
.EXAMPLE
.\eng\scripts\watch-mocks.ps1 -Once -WhatIf
.NOTES
Each detected HTML update opens a Chrome tab. CSS/JS-only edits do not trigger it.
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [string]$MocksPath,
    [ValidateRange(1, 3600)]
    [int]$IntervalSeconds = 3,
    [ValidateRange(0, 60)]
    [int]$SettleSeconds = 2,
    [string]$ChromePath,
    [switch]$Once
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if (-not $MocksPath) {
    $MocksPath = Join-Path $PSScriptRoot '../../docs/mocks'
}
$MocksPath = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($MocksPath)

if (-not $ChromePath) {
    $chromeCommand = Get-Command chrome.exe -ErrorAction SilentlyContinue
    if ($chromeCommand) {
        $ChromePath = $chromeCommand.Source
    } else {
        foreach ($basePath in @($env:ProgramFiles, ${env:ProgramFiles(x86)}, $env:LOCALAPPDATA)) {
            if ($basePath) {
                $candidate = Join-Path $basePath 'Google/Chrome/Application/chrome.exe'
                if (Test-Path -LiteralPath $candidate -PathType Leaf) {
                    $ChromePath = $candidate
                    break
                }
            }
        }
    }
}

if (-not $ChromePath -or -not (Test-Path -LiteralPath $ChromePath -PathType Leaf)) {
    throw 'Chrome was not found. Supply its executable path with -ChromePath.'
}

$lastOpened = $null
Write-Host "Watching $MocksPath every $IntervalSeconds seconds. Press Ctrl+C to stop."

do {
    try {
        $latest = $null
        if (Test-Path -LiteralPath $MocksPath -PathType Container) {
            $latest = Get-ChildItem -LiteralPath $MocksPath -Recurse -File |
                Where-Object { $_.Extension -in '.html', '.htm' } |
                Sort-Object -Property @{ Expression = 'LastWriteTimeUtc'; Descending = $true }, FullName |
                Select-Object -First 1
        }

        if ($latest) {
            $signature = '{0}|{1}|{2}' -f $latest.FullName, $latest.LastWriteTimeUtc.Ticks, $latest.Length
            $settled = ([DateTime]::UtcNow - $latest.LastWriteTimeUtc).TotalSeconds -ge $SettleSeconds
            if ($signature -ne $lastOpened -and $settled) {
                $url = ([Uri]$latest.FullName).AbsoluteUri
                if ($PSCmdlet.ShouldProcess($latest.FullName, 'Open HTML mock in Chrome')) {
                    Start-Process -FilePath $ChromePath -ArgumentList @('--new-tab', ('"{0}"' -f $url)) | Out-Null
                    Write-Host ('[{0:HH:mm:ss}] Opened {1}' -f (Get-Date), $latest.FullName)
                }
                $lastOpened = $signature
            }
        } elseif ($Once) {
            Write-Host 'No HTML mocks found yet.'
        }
    } catch {
        if ($Once) { throw }
        Write-Warning "Could not check or open the latest mock; retrying next poll. $($_.Exception.Message)"
    }

    if (-not $Once) { Start-Sleep -Seconds $IntervalSeconds }
} while (-not $Once)
