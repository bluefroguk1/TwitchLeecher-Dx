# Test script for the "fix-search-error" branch.
# Windows:  powershell -ExecutionPolicy Bypass -File test-scripts\test-fix-search-error.ps1
param([switch]$SkipBuild)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$proj = Join-Path $root (Join-Path 'TwitchLeecher' (Join-Path 'TwitchLeecher' 'TwitchLeecher.csproj'))
$out = Join-Path (Split-Path -Parent $proj) (Join-Path 'bin' (Join-Path 'Release' 'net10.0'))
$dll = Join-Path $out 'TwitchLeecher.dll'
$results = [ordered]@{}

function Start-App { Start-Process dotnet -ArgumentList "`"$dll`"" -PassThru }

function Ask($id, $text) {
    Write-Host ''
    Write-Host "[$id]" -ForegroundColor Cyan
    Write-Host $text
    do { $a = Read-Host 'Result? (y = pass, n = fail, s = skip)' } until ($a -match '^[yns]$')
    $results[$id] = @{ y = 'PASS'; n = 'FAIL'; s = 'SKIP' }[$a]
}

if (-not $SkipBuild) {
    dotnet build $proj -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
}
if (-not (Test-Path $dll)) { throw "App not found at $dll" }

$p = Start-App
Ask 'Failed search shows error once' @'
In the search page, make the search fail (disconnect your network, or enter a channel that does not exist), then click Search.
Expected: ONE error dialog, and you stay on the search page (no results page, no second error).
'@
Ask 'Search recovers' 'Reconnect / enter a valid channel and search again. Expected: results page shows normally.'

Write-Host 'Close Twitch Leecher to finish...' -ForegroundColor Yellow
if (-not $p.HasExited) { $p.WaitForExit() }
$report = ($results.GetEnumerator() | ForEach-Object { '{0,-40} {1}' -f $_.Key, $_.Value }) -join [Environment]::NewLine
Write-Host ''
Write-Host '===== Results =====' -ForegroundColor Green
Write-Host $report
