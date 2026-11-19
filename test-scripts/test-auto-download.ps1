# Test script for channel monitoring (auto download).
# Builds the app, then walks you through each check. Reads the auto download log and seen-list for you.
# Windows:  powershell -ExecutionPolicy Bypass -File test-scripts\test-auto-download.ps1
# Linux:    pwsh test-scripts/test-auto-download.ps1   (needs ffmpeg on PATH)
param([switch]$SkipBuild)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$proj = Join-Path $root (Join-Path 'TwitchLeecher' (Join-Path 'TwitchLeecher' 'TwitchLeecher.csproj'))
$out = Join-Path (Split-Path -Parent $proj) (Join-Path 'bin' (Join-Path 'Release' 'net10.0'))
$dll = Join-Path $out 'TwitchLeecher.dll'
$results = [ordered]@{}

$appData = Join-Path ([Environment]::GetFolderPath('ApplicationData')) 'Twitch Leecher DX'
$logFile = Join-Path $appData 'auto_download.log'
$seenFile = Join-Path $appData 'auto_download_seen.json'

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

function Show-Log {
    Write-Host ''
    Write-Host "--- $logFile ---" -ForegroundColor DarkGray
    if (Test-Path $logFile) { Get-Content $logFile -Tail 15 } else { Write-Host '(no log yet)' }
    Write-Host '---' -ForegroundColor DarkGray
}

# --- Build ---------------------------------------------------------------
if (-not $SkipBuild) {
    dotnet build $proj -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
}
if (-not (Test-Path $dll)) { throw "App not found at $dll" }

# --- 0. Fresh start ------------------------------------------------------
Write-Host ''
Write-Host 'Pick a channel that streams often and has recent VODs (e.g. a 24/7 or daily streamer).' -ForegroundColor Cyan
$channel = Read-Host 'Channel to watch [shroud]'
if (-not $channel) { $channel = 'shroud' }

if (Test-Path $seenFile) {
    $backup = "$seenFile.bak"
    Move-Item -Force $seenFile $backup
    Write-Host "Moved your existing seen-list to $backup so the first-run check starts clean."
}
if (Test-Path $logFile) { Remove-Item -Force $logFile }

$p = Start-App

# --- 1. Settings ---------------------------------------------------------
Ask 'Settings saved' @"
Open Settings > Auto Download and:
  - tick "Download new videos of these channels automatically"
  - Channels: $channel   (also try the star button: it adds your favourites)
  - Check every: 5 minutes  (try 4: it should not go below 5)
  - leave "Also clips" unticked for now
Save, close Settings and open it again. Are all values kept?
"@

# --- 2. First run marks existing videos as seen --------------------------
Write-Host ''
Write-Host 'The first check runs within about 1 minute of saving. Waiting 90s...' -ForegroundColor Cyan
Start-Sleep -Seconds 90
Show-Log
if (Test-Path $seenFile) { Write-Host "Seen-list: $((Get-Content $seenFile -Raw).Length) bytes, keys: $(((Get-Content $seenFile -Raw | ConvertFrom-Json).PSObject.Properties.Name) -join ', ')" }
Ask 'First run = mark as seen, no downloads' @"
Expected:
  - the log above has "$channel`: now watching, N existing VOD(s) marked as seen, not downloaded"
  - the Downloads page has NOTHING new from $channel
  - auto_download_seen.json exists and has a key for $($channel.ToLower())
"@

# --- 3. Restart does not re-download -------------------------------------
Wait-AppClosed $p
$p = Start-App
Write-Host 'Restarted. Waiting 90s for the next check...' -ForegroundColor Cyan
Start-Sleep -Seconds 90
Show-Log
Ask 'Restart: nothing re-queued' 'After the restart, is the Downloads page still empty for this channel, with no new "queued" lines in the log?'

# --- 4. A new VOD gets queued --------------------------------------------
# Simulates a new VOD by forgetting one seen id; a real new VOD needs the channel to stream.
Wait-AppClosed $p
Write-Host "Open $seenFile in Notepad, delete ONE id from the `"$($channel.ToLower())`" list (keep the JSON valid) and save." -ForegroundColor Cyan
Write-Host 'Pick a VOD that is NOT live and is short if you can.'
Read-Host 'Press Enter when saved'
$p = Start-App
Ask 'New VOD gets queued' @"
Within about a minute of the app starting, expected:
  - the Downloads page shows that VOD queued with your default quality/folder/filename
  - an in-app notification "Auto download: $channel - <title>"
  - a "queued VOD" line in the log
(If $channel is live right now, the live VOD is never queued until the stream ends: a "queued" line for it is a FAIL.)
"@
Show-Log

# --- 5. Clips -------------------------------------------------------------
Ask 'Clips with minimum views' @"
In Settings tick "Also clips with at least" and set 1000 views. Save.
Expected within ~1 min: the log says "$channel`: now watching, N existing clip(s) marked as seen" (clips have their own first run, so nothing is downloaded).
"@
Show-Log

# --- 6. Errors do not stop the loop --------------------------------------
Ask 'Bad channel / offline survive' @"
Add a channel that does not exist (e.g. zzzz_no_such_channel_zzzz) to the list, save, and wait ~1 min.
Expected: the log has "check failed, retrying next time" for that channel, $channel is still checked, the app stays responsive.
Optional: disconnect the network for one check; the log shows the error and the next check after reconnecting works.
"@
Show-Log

# --- 7. Off means off -----------------------------------------------------
Ask 'Switching off stops checks' 'Untick auto download and save. Wait 6 minutes (or skip): no new lines appear in the log.'

# --- 8. Quick download still works (moved code) ---------------------------
Ask 'Quick download + Download Selected unchanged' 'Search any channel. With Settings > Download > Quick download on, click Download on a VOD: it is queued straight away. Tick two VODs and use "Download Selected": both are queued.'

# --- Summary ---------------------------------------------------------------
if (-not $p.HasExited) { Wait-AppClosed $p }
$report = ($results.GetEnumerator() | ForEach-Object { '{0,-45} {1}' -f $_.Key, $_.Value }) -join [Environment]::NewLine
$file = Join-Path ([IO.Path]::GetTempPath()) "twitchleecher-auto-download-results-$(Get-Date -Format yyyyMMdd-HHmm).txt"
Set-Content -Path $file -Value $report
Write-Host ''
Write-Host '===== Results =====' -ForegroundColor Green
Write-Host $report
Write-Host ''
Write-Host "Saved to $file"
Write-Host "Your old seen-list (if any) is at $seenFile.bak; restore it by hand if you want it back."
