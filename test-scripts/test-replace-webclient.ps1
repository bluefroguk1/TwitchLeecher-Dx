# Test script for the "replace-webclient" branch.
# Builds the app, then walks you through manual checks of every network path (API, playlist, chunks, update check).
# Windows:  powershell -ExecutionPolicy Bypass -File test-scripts\test-replace-webclient.ps1
# Linux:    pwsh test-scripts/test-replace-webclient.ps1   (needs ffmpeg on PATH)
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
Ask 'Search' 'Search a channel by name and load its VODs. Titles, thumbnails and dates should appear as before. A channel name that does not exist should be reported, not crash.'
Ask 'Single VOD' 'Search by a VOD id or URL. The VOD should load. A non-existent VOD id should report not found, not crash.'
Ask 'Download' 'Download a 2-minute cropped VOD. It should finish with a playable file and no "missing frames" warning.'
Ask 'Update check' 'Run the update check (Info page, or restart with update checking enabled). No error dialog should appear.'
Ask 'Retries (optional)' 'Start a longer download and disconnect the network briefly. It should log retries and recover after reconnecting, as before. Answer s to skip.'
Ask 'Authentication (optional)' 'If you use a Twitch login: sign out and back in. Answer s if not applicable.'
Wait-AppClosed $p

Write-Host ''
Write-Host '--- Results ---' -ForegroundColor Green
$results.GetEnumerator() | ForEach-Object { Write-Host ("{0,-28} {1}" -f $_.Key, $_.Value) }
