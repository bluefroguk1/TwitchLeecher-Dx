# Test script for the "ffmpeg-update" branch (bundled ffmpeg 5.0 -> 9.0.2).
# Builds the app, checks the bundled ffmpeg version, then walks you through a short VOD download.
# Windows:  powershell -ExecutionPolicy Bypass -File test-scripts\test-ffmpeg-update.ps1
param([switch]$SkipBuild)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$proj = Join-Path $root (Join-Path 'TwitchLeecher' (Join-Path 'TwitchLeecher' 'TwitchLeecher.csproj'))
$out = Join-Path (Split-Path -Parent $proj) (Join-Path 'bin' (Join-Path 'Release' 'net10.0'))
$dll = Join-Path $out 'TwitchLeecher.dll'
$results = [ordered]@{}

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

# --- 1. Bundled ffmpeg version (automatic) --------------------------------
$ffmpeg = Join-Path $out 'ffmpeg.exe'
$ver = (& $ffmpeg -version | Select-Object -First 1)
Write-Host "Bundled ffmpeg: $ver"
$results['Bundled ffmpeg is 9.x'] = $(if ($ver -match 'version 9\.') { 'PASS' } else { "FAIL ($ver)" })

# --- 2. Short VOD download ------------------------------------------------
Write-Host ''
Write-Host 'Starting Twitch Leecher. Close it when you are done with the checks.' -ForegroundColor Yellow
$p = Start-Process dotnet -ArgumentList "`"$dll`"" -PassThru

Ask 'download starts' 'Search a channel, click Download on any VOD. Tick Crop Start 00:00:00 and End 00:02:00, conversion to mp4 enabled, and start the download.'
Ask 'progress bar' 'While "Converting Video" runs, does the progress bar move from 0 to 100% (or at least leave the indeterminate state)? A very short conversion may finish too fast to see; answer s then.'
Ask 'result plays' 'Does the status reach Done, and does the resulting .mp4 play (about 2 minutes, with video and sound)?'
Ask 'log is clean' 'Open the download log (Downloads > log icon). Is there no ffmpeg error such as "Unrecognized option" or "Option not found"?'

if (-not $p.HasExited) { Write-Host 'Close Twitch Leecher to finish...' -ForegroundColor Yellow; $p.WaitForExit() }

Write-Host ''
Write-Host '=== Results ===' -ForegroundColor Green
$results.GetEnumerator() | ForEach-Object { Write-Host ('{0,-30} {1}' -f $_.Key, $_.Value) }
