# Test script for the "direct-to-ffmpeg" branch.
# Builds the app, then walks you through the manual download checks.
# Windows:  powershell -ExecutionPolicy Bypass -File test-scripts\test-direct-to-ffmpeg.ps1
# Linux:    pwsh test-scripts/test-direct-to-ffmpeg.ps1   (needs ffmpeg on PATH)
param([switch]$SkipBuild)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$proj = Join-Path $root (Join-Path 'TwitchLeecher' (Join-Path 'TwitchLeecher' 'TwitchLeecher.csproj'))
$out = Join-Path (Split-Path -Parent $proj) (Join-Path 'bin' (Join-Path 'Release' 'net10.0'))
$dll = Join-Path $out 'TwitchLeecher.dll'
$results = [ordered]@{}

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

if (-not $SkipBuild) {
    dotnet build $proj -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
}
if (-not (Test-Path $dll)) { throw "App not found at $dll" }

Write-Host ''
Write-Host 'Starting the app. Run the downloads below, then close the app. While a download runs, watch the' -ForegroundColor Cyan
Write-Host 'temp download folder: the big merged .ts file should NOT appear (except in checks 3 and 4).' -ForegroundColor Cyan
$p = Start-Process dotnet -ArgumentList "`"$dll`"" -PassThru
Wait-AppClosed $p

Ask 'Uncropped short download' 'Download a short VOD with no cropping. Does it finish and play correctly (audio + video, full length)? Was there no merged .ts in the temp folder during conversion?'
Ask 'Crop end only' 'Download a VOD with only an end crop (e.g. first 2 minutes). Is the length right and the file playable? No merged .ts in the temp folder?'
Ask 'Crop start (old path)' 'Download with a start crop (and optionally an end crop). Is the start offset correct? (This case still uses the merged .ts by design; the log should show a "Merging all VOD parts" line.)'
Ask 'Disable conversion' 'Download with "Disable conversion" ticked. Is the output a playable .ts file, as before?'
Ask 'Init segment VOD' 'Download a VOD whose log shows "Downloading VOD init segment..." (newer fMP4 streams), uncropped. Does the mp4 play with audio and video for the full length? Repeat with end crop only.'
Ask 'Cancel mid-download' 'Start a download and cancel while chunks are downloading. Does it show Canceled and clean up the temp folder?'
Ask 'Cancel during conversion' 'Start a longer uncropped download, and cancel once the status shows "Converting Video" (if the button is available). Is ffmpeg gone from Task Manager and no partial output left?'
Ask 'Conversion failure message' 'Optional: set the output folder to an unwritable location after the download starts, or otherwise force ffmpeg to fail. Does the download show the Error state with ffmpeg log lines?'

Write-Host ''
Write-Host 'RESULTS' -ForegroundColor Cyan
$results.GetEnumerator() | ForEach-Object { Write-Host ("{0,-40} {1}" -f $_.Key, $_.Value) }
