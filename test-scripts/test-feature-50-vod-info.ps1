# Test script for the "feature-50-vod-info" branch (save VOD info, issue #50).
# Builds the app, starts it, then walks you through each manual check.
# Windows:  powershell -ExecutionPolicy Bypass -File test-scripts\test-feature-50-vod-info.ps1
# Linux:    pwsh test-scripts/test-feature-50-vod-info.ps1   (needs ffmpeg on PATH)
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
Ask 'Checkbox present, off by default' 'Search any channel and click Download on a VOD. Is there a "Save VOD info (.info.json)" checkbox next to "Use Custom Filename", unticked?'
Ask 'Info file written' @'
Tick "Save VOD info", crop End to 00:01:00, download. When it is Done, look in the download folder.
Expected: next to the video there is <same name>.info.json containing title, channel, id, url, recordedDate, length, quality, cropEnd and videoFile matching the VOD.
'@
Ask 'Not written when unticked' 'Download another short cropped VOD with the box UNticked. Expected: no .info.json is created.'
Ask 'Long filename case (issue #50)' 'Use a filename template containing the title and download with the box ticked. Expected: the title is complete in the .info.json even if the video filename is shortened.'

# --- Summary ---------------------------------------------------------------
if (-not $p.HasExited) { Wait-AppClosed $p }
$report = ($results.GetEnumerator() | ForEach-Object { '{0,-40} {1}' -f $_.Key, $_.Value }) -join [Environment]::NewLine
$file = Join-Path ([IO.Path]::GetTempPath()) "twitchleecher-feature-50-vod-info-results-$(Get-Date -Format yyyyMMdd-HHmm).txt"
Set-Content -Path $file -Value $report
Write-Host ''
Write-Host '===== Results =====' -ForegroundColor Green
Write-Host $report
Write-Host ''
Write-Host "Saved to $file"
