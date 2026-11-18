# Test script for the "feature-86-default-favourites" branch (Favourites as default search, issue #86).
# Builds the app, starts it, then walks you through each manual check.
# Windows:  powershell -ExecutionPolicy Bypass -File test-scripts\test-feature-86-default-favourites.ps1
# Linux:    pwsh test-scripts/test-feature-86-default-favourites.ps1   (needs ffmpeg on PATH)
param([switch]$SkipBuild)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$proj = Join-Path $root (Join-Path 'TwitchLeecher' (Join-Path 'TwitchLeecher' 'TwitchLeecher.csproj'))
$out = Join-Path (Split-Path -Parent $proj) (Join-Path 'bin' (Join-Path 'Release' 'net10.0'))
$dll = Join-Path $out 'TwitchLeecher.dll'
$results = [ordered]@{}

function Start-App { Start-Process dotnet -ArgumentList "`"$dll`"" -PassThru }

function Wait-AppClosed($p) {
    Write-Host 'Close Twitch Leecher to continue...' -ForegroundColor Yellow
    $p.WaitForExit()
}

function Ask($id, $text) {
    Write-Host ''
    Write-Host "[$id]" -ForegroundColor Cyan
    Write-Host $text
    do { $a = Read-Host 'Result? (y = pass, n = fail, s = skip)' } until ($a -match '^[yns]$')
    $results[$id] = @{ y = 'PASS'; n = 'FAIL'; s = 'SKIP' }[$a]
}

# --- Build ---------------------------------------------------------------
if (-not $SkipBuild) {
    dotnet build $proj -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
}
if (-not (Test-Path $dll)) { throw "App not found at $dll" }

$p = Start-App
Write-Host 'The app has started.' -ForegroundColor Yellow

# --- Checks ----------------------------------------------------------------
Ask 'Default is unchanged' 'Open Settings > Search. Is there a new "Use Favourites as the default search" checkbox, unticked? With it unticked, does the Search page open on the Channel tab as before?'
Ask 'Search page starts on Favorites' @'
In Settings > Search add at least two favourite channels (+ button), tick "Use Favourites as the default search" and save.
Restart the app (close it first), open Search. Expected: the Favorites tab is selected; pressing Search lists VODs from all favourites.
'@
Ask 'Search on Start uses favourites' @'
Also tick "Search on Start" and clear the Favourite Channels text box (no single default channel). Save.
Expected: no validation error is shown; after restarting, the app goes straight to results from all favourite channels.
'@
Ask 'Setting persists' 'Untick the favourites option, save, restart. Expected: the Search page opens on the Channel tab again and the checkbox stays unticked.'

# --- Summary ---------------------------------------------------------------
if (-not $p.HasExited) { Wait-AppClosed $p }
$report = ($results.GetEnumerator() | ForEach-Object { '{0,-40} {1}' -f $_.Key, $_.Value }) -join [Environment]::NewLine
$file = Join-Path ([IO.Path]::GetTempPath()) "twitchleecher-feature-86-default-favourites-results-$(Get-Date -Format yyyyMMdd-HHmm).txt"
Set-Content -Path $file -Value $report
Write-Host ''
Write-Host '===== Results =====' -ForegroundColor Green
Write-Host $report
Write-Host ''
Write-Host "Saved to $file"
