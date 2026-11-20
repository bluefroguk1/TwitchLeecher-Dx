# Test script for the "improve-error-logging" branch.
# Builds the app, then walks you through manual checks of the swallowed-exception log.
# Windows:  powershell -ExecutionPolicy Bypass -File test-scripts\test-improve-error-logging.ps1
# Linux:    pwsh test-scripts/test-improve-error-logging.ps1   (needs ffmpeg on PATH)
param([switch]$SkipBuild)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$proj = Join-Path $root (Join-Path 'TwitchLeecher' (Join-Path 'TwitchLeecher' 'TwitchLeecher.csproj'))
$out = Join-Path (Split-Path -Parent $proj) (Join-Path 'bin' (Join-Path 'Release' 'net10.0'))
$dll = Join-Path $out 'TwitchLeecher.dll'
$logFile = Join-Path ([Environment]::GetFolderPath('ApplicationData')) (Join-Path 'TwitchLeecher' (Join-Path 'logs' 'swallowed.log'))
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

# Start from a clean log so the entries below are attributable to this run
if (Test-Path $logFile) { Remove-Item $logFile -Force }

$p = Start-App
Ask 'Search' 'Search a channel by name and load its VODs. Results should appear as before.'
Ask 'Download' 'Download a 2-minute cropped VOD. It should finish and play as before.'
Ask 'Update check' 'Open Info and run the update check (or restart with update checking enabled). No error dialog should appear.'
Ask 'No new dialogs' 'Did the app behave exactly as before, with no new popups or crashes?'
Wait-AppClosed $p

# Make a malformed setting so a swallowed parse failure is recorded
Write-Host ''
Write-Host "Optional: edit %APPDATA%\TwitchLeecher\config.xml, set a <CheckForUpdates> value to 'banana', start and close the app." -ForegroundColor Yellow
Ask 'Log text' "Open $logFile. It should exist, with timestamped lines such as 'PreferencesService: preferences.AppCheckForUpdates: FormatException ...' (skip if you did not edit the config, but any entry should be readable and repeats should not flood it)."

Write-Host ''
Write-Host '--- Results ---' -ForegroundColor Green
$results.GetEnumerator() | ForEach-Object { Write-Host ("{0,-16} {1}" -f $_.Key, $_.Value) }
