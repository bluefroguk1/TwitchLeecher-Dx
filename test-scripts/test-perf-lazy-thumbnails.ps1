# Test script for the "perf-lazy-thumbnails" branch.
# Windows:  powershell -ExecutionPolicy Bypass -File test-scripts\test-perf-lazy-thumbnails.ps1
param([switch]$SkipBuild)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$proj = Join-Path $root (Join-Path 'TwitchLeecher' (Join-Path 'TwitchLeecher' 'TwitchLeecher.csproj'))
$out = Join-Path (Split-Path -Parent $proj) (Join-Path 'bin' (Join-Path 'Release' 'net10.0'))
$dll = Join-Path $out 'TwitchLeecher.dll'
$results = [ordered]@{}

function Start-App { Start-Process dotnet -ArgumentList "`"$dll`"" -PassThru }
function Wait-AppClosed($p) { Write-Host 'Close Twitch Leecher to continue...' -ForegroundColor Yellow; $p.WaitForExit() }
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

Ask 'Placeholders then thumbnails' @'
Search for a channel with many VODs (50+ results).
Expected: result cards appear immediately with their placeholder, thumbnails fill in progressively (a few at a time) and the UI stays responsive.
'@

Ask 'Scrolling loads thumbnails' @'
Scroll down through the whole result list.
Expected: cards that scroll into view get their thumbnail shortly after; none stay blank permanently (apart from VODs Twitch has no thumbnail for).
'@

Ask 'Downloads list thumbnail' @'
Queue a download and open the Downloads view.
Expected: the download entry shows the VOD thumbnail as before.
'@

Wait-AppClosed $p

$report = ($results.GetEnumerator() | ForEach-Object { '{0,-40} {1}' -f $_.Key, $_.Value }) -join [Environment]::NewLine
$file = Join-Path ([IO.Path]::GetTempPath()) "twitchleecher-perf-lazy-thumbnails-results-$(Get-Date -Format yyyyMMdd-HHmm).txt"
Set-Content -Path $file -Value $report
Write-Host ''
Write-Host '===== Results =====' -ForegroundColor Green
Write-Host $report
Write-Host "Saved to $file"
