# Test script for the "ui-download-dialog" branch (redesigned Download dialog with preview filmstrip).
# Instant UI checks first, then one short cropped download.
# Windows:  powershell -ExecutionPolicy Bypass -File test-scripts\test-ui-download-dialog.ps1
# Linux:    pwsh test-scripts/test-ui-download-dialog.ps1   (needs ffmpeg on PATH)
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

# --- 1. Layout (instant) ----------------------------------------------------
Ask 'Header' 'Search any channel and click Download on a past broadcast (VOD). Does the dialog show the thumbnail, title and "channel · date · length" on top?'
Ask 'Quality + Save to' 'Are QUALITY and SAVE TO side by side, and does "Change" open a folder picker that updates the folder?'
Ask 'Footer buttons' 'Are Cancel and a purple "Download" button at the bottom right, and is there NO Download/Cancel in the top menu bar? Does Esc cancel?'
Ask 'Extras' 'Under EXTRAS: "Save chat as text" and "Save video details", one per line, no ".txt"/".json" shown?'

# --- 2. Previews and range (instant) ----------------------------------------
Ask 'Filmstrip' 'Under PART TO DOWNLOAD: within a second or two, do START/END pictures and a strip of thumbnails across the whole video appear?'
Ask 'Drag handles' 'Drag the left and right white handles. Do the START/END pictures and timecodes, the From/to fields and the "length · ~size" readout all follow? Is the area outside the selection darker?'
Ask 'Handles min gap' 'Drag the handles into each other. Do they stop about 5 seconds apart instead of crossing?'
Ask 'Type times' 'Type 00:01:00 in From and 00:03:00 in To, then click elsewhere. Do the handles and pictures move there, and does the readout say "02:00 · ~..."?'
Ask 'Bad time' 'Type "abc" (or a time past the video end) in From. Does the field show a red error instead of crashing?'
Ask 'Whole video' 'Click "Whole video". Do the handles go to both ends and the fields show 00:00:00 and the full length?'

# --- 3. File name (instant) -------------------------------------------------
Ask 'File name follows crop' 'With your file name template using crop wildcards (e.g. {crop_start}) - or any template - change quality or drag a handle. Does the file name update?'
Ask 'File name custom + reset' 'Type your own file name, then drag a handle. Does your name stay? Then click the circular-arrow button: does it go back to the template name?'

# --- 4. Fallbacks (instant) -------------------------------------------------
Ask 'Clip: no crop section' 'Search a clip URL (Search > URLs) and click Download. Is the PART TO DOWNLOAD section hidden, and "Save chat as text" hidden? (s if you have no clip to hand)'
Ask 'Conversion off: no crop section' 'In Settings tick "Disable conversion", open the Download dialog on a VOD: is PART TO DOWNLOAD hidden? Untick it again afterwards.'
Ask 'Muted banner' 'Open Download on a VOD with muted parts (often ones with music). Is there one yellow line "Some parts of this video are muted by Twitch." and does "What does this mean?" show an explanation? (s if none to hand)'
Ask 'No storyboard (optional)' 'If you find a VOD whose filmstrip never appears: is the plain dark range bar still usable (handles drag, fields work) with no START/END pictures? (s if all had previews)'

# --- 5. Quick download --------------------------------------------------------
Ask 'Cropped download' @'
Set From 00:01:00, To 00:02:00 with the handles or fields, click Download.
Expected: status reaches Done and the file plays about 1 minute starting at the 1:00 mark.
'@

# --- Summary ---------------------------------------------------------------
if (-not $p.HasExited) { Wait-AppClosed $p }
$report = ($results.GetEnumerator() | ForEach-Object { '{0,-40} {1}' -f $_.Key, $_.Value }) -join [Environment]::NewLine
$file = Join-Path ([IO.Path]::GetTempPath()) "twitchleecher-ui-download-dialog-results-$(Get-Date -Format yyyyMMdd-HHmm).txt"
Set-Content -Path $file -Value $report
Write-Host ''
Write-Host '===== Results =====' -ForegroundColor Green
Write-Host $report
Write-Host ''
Write-Host "Saved to $file"
