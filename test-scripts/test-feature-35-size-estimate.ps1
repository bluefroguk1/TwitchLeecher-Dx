# Test script for the "feature-35-size-estimate" branch (estimated file size, issue #35).
# Builds the app, starts it, then walks you through each manual check.
# Windows:  powershell -ExecutionPolicy Bypass -File test-scripts\test-feature-35-size-estimate.ps1
# Linux:    pwsh test-scripts/test-feature-35-size-estimate.ps1   (needs ffmpeg on PATH)
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
Ask 'Estimate shown' 'Search any channel and click Download on a VOD. Next to the Quality box, is there text like "Est. size: ~1.85 GB" (marked as an estimate; the tooltip says so too)?'
Ask 'Updates with quality' 'Switch the quality from Source to a lower one and to Audio only. Does the estimate drop each time (audio only should be small)?'
Ask 'Updates with crop' 'Tick Crop Start/End and shorten the range to a few minutes. Does the estimate shrink in proportion to the length (half the length = half the size)?'
Ask 'Roughly matches reality' 'Download a short cropped range (e.g. 2 minutes) at Source quality. Is the real file size within roughly 50-100% of the estimate? (It should not be larger by a wide margin; it is an upper-ish estimate.)'

# --- Summary ---------------------------------------------------------------
if (-not $p.HasExited) { Wait-AppClosed $p }
$report = ($results.GetEnumerator() | ForEach-Object { '{0,-40} {1}' -f $_.Key, $_.Value }) -join [Environment]::NewLine
$file = Join-Path ([IO.Path]::GetTempPath()) "twitchleecher-feature-35-size-estimate-results-$(Get-Date -Format yyyyMMdd-HHmm).txt"
Set-Content -Path $file -Value $report
Write-Host ''
Write-Host '===== Results =====' -ForegroundColor Green
Write-Host $report
Write-Host ''
Write-Host "Saved to $file"
