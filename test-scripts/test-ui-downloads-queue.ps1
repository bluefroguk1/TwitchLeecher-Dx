# Test script for the "ui-downloads-queue" branch (redesigned Downloads queue).
# Windows:  powershell -ExecutionPolicy Bypass -File test-scripts\test-ui-downloads-queue.ps1
param([switch]$SkipBuild)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$proj = Join-Path $root (Join-Path 'TwitchLeecher' (Join-Path 'TwitchLeecher' 'TwitchLeecher.csproj'))
$out = Join-Path (Split-Path -Parent $proj) (Join-Path 'bin' (Join-Path 'Release' 'net10.0'))
$exe = Join-Path $out 'TwitchLeecher.exe'
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
if (-not (Test-Path $exe)) { throw "App not found at $exe" }
$p = Start-Process $exe -PassThru

# --- Instant (UI only) -------------------------------------------------------
Ask 'Empty state' 'Open Downloads with nothing queued. Do you see "No downloads" in the summary bar and the "No downloads yet" hint, in the dark redesign colours?'

# --- Quick downloads (crop each VOD to 00:00:00-00:02:00) --------------------
Ask 'Card + speed line' @'
Queue 3 short cropped VODs. On the running card: thumbnail, title, purple DOWNLOADING chip, "channel . ~size" line,
progress bar, and "NN% . X MB/s . N min left" updating about once a second. Buttons: Pause, Cancel.
'@
Ask 'Summary bar' 'Does the summary show counts ("1 downloading . 2 queued"), "X of Y GB . about N min left", and a thin progress bar that grows?'
Ask 'Queued buttons + Move up' 'Queued cards show "Starts after the current download", Move up and Remove. Move up on the last queued card puts it above the other queued one, and it then starts first.'
Ask 'Pause / resume active' @'
Pause the running download. It shows "Pausing after the chunks in flight", then a PAUSED chip with "Paused at NN%"
(Resume, Remove). The next queued item starts. Resume it: it continues from about the same % (does not restart at 0) and finishes with a playable file.
'@
Ask 'Pause all / Resume all' 'Pause all pauses every queued/running item and the button becomes "Resume all"; Resume all queues them again.'
Ask 'Done buttons' 'A finished card shows a green DONE chip, "Saved to <folder>", Play (opens the video in your player) and Open folder.'
Ask 'Failed reason' @'
Set the download folder to a path you cannot write (e.g. C:\Windows\System32\tl-test) or disconnect the network mid-download.
The card shows a red FAILED chip with a short reason ("Disk error: ..." / "Network error"), plus Retry and View log. Retry works once fixed.
'@
Ask 'Clear finished' 'Clear finished removes done, failed and canceled cards and leaves queued/paused ones.'

Ask 'Theme switch New <-> Original' @'
With at least one downloading, one paused, one done and one failed card on screen, switch Settings theme New -> Original and back.
Do the summary bar, cards, chips, progress bars and buttons follow each theme (Original uses the yellow accent), with all text readable?
'@

# --- Long VOD -----------------------------------------------------------------
Ask 'Long VOD ETA (optional)' 'Download an uncropped multi-hour VOD for a few minutes. Is the speed steady (smoothed) and the time left plausible? Answer s to skip.'

if (-not $p.HasExited) { Write-Host 'Close Twitch Leecher to finish.' -ForegroundColor Yellow; $p.WaitForExit() }
$report = ($results.GetEnumerator() | ForEach-Object { '{0,-30} {1}' -f $_.Key, $_.Value }) -join [Environment]::NewLine
$file = Join-Path ([IO.Path]::GetTempPath()) "twitchleecher-ui-downloads-queue-results-$(Get-Date -Format yyyyMMdd-HHmm).txt"
Set-Content -Path $file -Value $report
Write-Host ''; Write-Host '===== Results =====' -ForegroundColor Green; Write-Host $report; Write-Host "Saved to $file"
