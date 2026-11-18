# Test script for the "feature-96-qol" branch (#96 power-user improvements):
#   a) search results keep their scroll position after the download dialog
#   b) "Quick download" setting skips the download dialog
#   c) tick several results and use "Download Selected"
# Windows:  powershell -ExecutionPolicy Bypass -File test-scripts\test-feature-96-qol.ps1
# Linux:    pwsh test-scripts/test-feature-96-qol.ps1   (needs ffmpeg on PATH)
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

Write-Host ''
Write-Host 'Tip: pause the download queue (Downloads > Pause) if you do not want these test downloads to actually run,' -ForegroundColor Yellow
Write-Host 'or remove them from the queue afterwards.' -ForegroundColor Yellow
$p = Start-Process dotnet -ArgumentList "`"$dll`"" -PassThru

# --- a) scroll position -----------------------------------------------------
Ask '#96a scroll position kept' @'
Search a channel with lots of VODs (load limit 50+). Scroll well down the results.
Click the download button on a VOD, then Cancel in the download dialog.
Are you back at the same place in the list (not the top)?
Repeat, but this time click Download in the dialog. Still at the same place?
Also: Settings, then back to search results. Same place?
'@
Ask '#96a new search starts at top' 'Run a NEW search. Do the new results start at the top?'

# --- b) quick download ------------------------------------------------------
Ask '#96b quick download' @'
Settings > Download: tick "Quick download (skip the download dialog)" and save.
Back in the results, click the download button on a VOD.
Expected: no dialog, a "Download added" notification, the Downloads count goes up, and you stay at the same scroll position.
'@
Ask '#96b quick download duplicate' 'Click the download button on the SAME VOD again. Expected: notification "Already in the download queue" and no second copy in Downloads.'
Ask '#96b setting saved' 'Close and reopen the app (or just reopen Settings). Is "Quick download" still ticked? Then untick it and check the download button shows the dialog again.'

# --- c) multi-select ------------------------------------------------------------
Ask '#96c download selected' @'
Tick the checkbox next to the download button on 3 VODs, then click "Download Selected" in the top menu.
Expected: the window stays responsive while it works, then "3 download(s) added", the 3 boxes are unticked, and 3 new entries are in Downloads using your default quality/folder/filename.
'@
Ask '#96c skipped items explained' @'
Tick one VOD you already queued plus one new VOD, then "Download Selected".
Expected: a message saying 1 added and listing the already-queued one as skipped (it stays ticked).
'@
Ask '#96c nothing selected' 'With nothing ticked, click "Download Selected". Expected: a notification asking you to tick videos first.'
Ask '#96c live streams' 'If the channel is live: the live entry''s checkbox is disabled, like its download button. (s if not live)'

if (-not $p.HasExited) { Write-Host 'Close Twitch Leecher to finish.' -ForegroundColor Yellow; $p.WaitForExit() }
$report = ($results.GetEnumerator() | ForEach-Object { '{0,-40} {1}' -f $_.Key, $_.Value }) -join [Environment]::NewLine
$resultFile = Join-Path ([IO.Path]::GetTempPath()) "twitchleecher-qol-results-$(Get-Date -Format yyyyMMdd-HHmm).txt"
Set-Content -Path $resultFile -Value $report
Write-Host ''
Write-Host '===== Results =====' -ForegroundColor Green
Write-Host $report
Write-Host "Saved to $resultFile"
