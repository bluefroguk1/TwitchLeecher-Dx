# Test script for the "fix-page-scroll" branch.
# Windows:  powershell -ExecutionPolicy Bypass -File test-scripts\test-fix-page-scroll.ps1
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
Ask 'Downloads page keeps scroll' @'
Have enough downloads that the Downloads page scrolls. Scroll down, open another page (Search or Settings), then return to Downloads.
Expected: same scroll position, not the top.
'@
Ask 'Settings page keeps scroll' @'
Shrink the window so Settings scrolls. Scroll down, go to another page, then return to Settings.
Expected: same scroll position, not the top.
'@
Ask 'Search results page still keeps scroll' 'Run a search, scroll down the results, go to Downloads and back. Expected: same position.'
Ask 'No jump on first open' 'Open each page for the first time after launch. Expected: starts at the top with no flicker or jump.'

Write-Host 'Close Twitch Leecher to finish...' -ForegroundColor Yellow
if (-not $p.HasExited) { $p.WaitForExit() }
$report = ($results.GetEnumerator() | ForEach-Object { '{0,-40} {1}' -f $_.Key, $_.Value }) -join [Environment]::NewLine
Write-Host ''
Write-Host '===== Results =====' -ForegroundColor Green
Write-Host $report
