# Test script for the "ui-watched-searches" branch (redesign branch 8: watched searches).
# Windows:  powershell -ExecutionPolicy Bypass -File test-scripts\test-ui-watched-searches.ps1
# Order: instant UI checks, then "Check now" on a channel with existing videos (first run), and last, optional,
# waiting for a channel to publish something new.
param([switch]$SkipBuild)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$proj = Join-Path $root (Join-Path 'TwitchLeecher' (Join-Path 'TwitchLeecher' 'TwitchLeecher.csproj'))
$out = Join-Path (Split-Path -Parent $proj) (Join-Path 'bin' (Join-Path 'Release' 'net10.0'))
$exe = Join-Path $out 'TwitchLeecher.exe'
$data = Join-Path $env:APPDATA 'Twitch Leecher DX'
$searchesFile = Join-Path $data 'watched_searches.json'
$seenFile = Join-Path $data 'auto_download_seen.json'
$logFile = Join-Path $data 'auto_download.log'
$results = [ordered]@{}

function Ask($id, $text) {
    Write-Host ''
    Write-Host "[$id]" -ForegroundColor Cyan
    Write-Host $text
    do { $a = Read-Host 'Result? (y = pass, n = fail, s = skip)' } until ($a -match '^[yns]$')
    $results[$id] = @{ y = 'PASS'; n = 'FAIL'; s = 'SKIP' }[$a]
}

# Simulates a new video: closes the app, drops the newest seen id of every search, starts the app again.
function Simulate-NewVideo {
    if (-not (Test-Path $seenFile)) { Write-Host 'No seen file yet; run a first check first.' -ForegroundColor Red; return }
    Write-Host 'Close Twitch Leecher if it is open (the app rewrites the seen file), then press Enter.' -ForegroundColor Yellow
    Read-Host | Out-Null
    $seen = Get-Content $seenFile -Raw | ConvertFrom-Json
    foreach ($prop in $seen.PSObject.Properties) {
        if ($prop.Value.Count -gt 0) {
            Write-Host "  $($prop.Name): removed $($prop.Value[0])"
            $prop.Value = @($prop.Value | Select-Object -Skip 1)
        }
    }
    $seen | ConvertTo-Json -Depth 5 | Set-Content $seenFile
    $script:p = Start-Process $exe -PassThru
    Write-Host 'Started again. Open Settings > Watched searches. (If "Check watched searches" is ticked, a check runs at start-up, so it may already have happened.)' -ForegroundColor Yellow
}

function Show-Log($lines) {
    if (Test-Path $logFile) {
        Write-Host '--- last lines of auto_download.log ---' -ForegroundColor DarkGray
        Get-Content $logFile -Tail $lines | ForEach-Object { Write-Host $_ }
    } else {
        Write-Host '(no auto_download.log yet)' -ForegroundColor DarkGray
    }
}

if (-not $SkipBuild) {
    dotnet build $proj -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
}
if (-not (Test-Path $exe)) { throw "App not found at $exe" }

# Migration only runs when watched_searches.json does not exist yet.
$firstLaunch = -not (Test-Path $searchesFile)
$oldChannels = $null
$configFile = Join-Path $data 'config.xml'
if ($firstLaunch -and (Test-Path $configFile)) {
    try { $oldChannels = ([xml](Get-Content $configFile)).SelectSingleNode('//AutoDownload/Channels').InnerText } catch { }
}

$p = Start-Process $exe -PassThru

# --- Migration (only on the first launch of this build) -----------------------
if ($firstLaunch -and $oldChannels) {
    Ask 'Migration' @"
Your old auto-download channel list was: $oldChannels
Open Settings and scroll to "Watched searches". Is there one "<channel> . Broadcasts" search per old channel,
plus a "<channel> . Clips" search (tag ">= N views") for each if clips were switched on? Is On/Off and the interval as before?
"@
} else {
    Write-Host 'Migration check skipped: watched_searches.json already exists or there was no old channel list.' -ForegroundColor DarkGray
}

# --- Instant (UI only) ---------------------------------------------------------
Ask 'Settings page layout' @'
Settings > Watched searches. It should look like the other sections (same heading, margins, grey SEARCHES / RECENT ACTIVITY
labels), with no dark boxes. Do you see: the heading; a tick "Check watched searches for new videos" with
"Checks only run while Twitch Leecher is open" under it; a line with a dot, "Last check ... / Not checked yet",
"Every [interval]" and "Check now"; SEARCHES with an accent "+ Add watched search"; searches as flat rows split by thin lines
(a row lights up on hover and stays lit while its editor is open); RECENT ACTIVITY as the same flat rows?
No "Auto Download" text anywhere?
'@
Ask 'Add + edit a search' @'
Press "+ Add watched search". A "New search . Broadcasts" row opens with an editor: Channel, Type, Quality, Remove, Done.
Type a channel, pick Type = Clips: "Minimum views" appears and the title/tags update (">= 100 views", "Default quality").
Pick Quality = 720p: the tag says 720p. Press Done: the editor closes.
'@
Ask 'When found + Active' @'
On a row, switch "When found" to "Add to queue, paused" and untick Active. The row's content fades and says "Search switched off".
Go to Downloads and back to Settings: both choices are kept. Tick Active again.
'@
Ask 'Saved straight away' @'
Without pressing Settings Save, close and restart Twitch Leecher (or leave Settings and press Undo first).
Are the searches, the "Check watched searches" tick and the interval still as you left them? (They live in %APPDATA%\Twitch Leecher DX\watched_searches.json.)
'@
Ask 'Remove' 'Edit a search and press Remove. Is it gone (and still gone after a restart)?'
Ask 'Watch this search (Results)' @'
Search a channel's Broadcasts. In the results menu press "Watch this search". A notification says "Watching <channel> . Broadcasts"
(and "watched searches switched on" if it was off). The search appears in Settings with default settings and "Check watched searches for new videos" is ticked.
Press it again: no duplicate. Then search by URL/ID and press it: "Only a channel search can be watched".
'@
Ask 'Downloads pill' @'
Open Downloads. The summary bar shows a pill "N watched searches" with a green dot (grey and "Watched searches off" when "Check watched searches" is unticked).
Click it: Settings opens scrolled to the Watched searches section.
'@
Ask 'Theme switch' 'Switch the Settings theme through New, Original and Coyote (if present) with the section and Downloads visible. Do the rows, row hover, divider lines, tags, pill and activity colours follow the theme and stay readable?'

# --- Check now: first run on a channel with existing videos --------------------
if (Test-Path $seenFile) {
    Move-Item $seenFile "$seenFile.bak" -Force
    Write-Host "Moved auto_download_seen.json to .bak so every search does a first run." -ForegroundColor Yellow
    Write-Host 'Close Twitch Leecher and start it again (it keeps the seen list in memory), then continue.' -ForegroundColor Yellow
}
Ask 'Check now = first run' @'
Add a Broadcasts search for a channel that already has VODs, Active, "Download now". Press "Check now".
Within a few seconds: the status says "Last check just now . next in ...", RECENT ACTIVITY shows a purple FIRST RUN line
"<channel> . Broadcasts . N existing videos marked as seen, not downloaded", the card says "First check ...",
and NOTHING was added to Downloads.
'@
Show-Log 8

Ask 'First run while the channel is live (optional)' @'
Only if a channel is streaming right now: add a Broadcasts search for it and press Check now. The FIRST RUN line ends with
"the current live stream will download when it ends". Later checks show a WAITING line while it is live, and once the stream
has ended the next check STARTS (or PAUSES) that VOD. Answer s to skip if no channel is live.
'@

Simulate-NewVideo
Ask 'Download now' @'
(Check the removed id is not a live VOD.) With "When found" = "Download now", press Check now. RECENT ACTIVITY shows a green
STARTED line with the video title, a notification "Watched search ...", and Downloads has the item running or queued with a
"DOWNLOADING . WATCHED" / "QUEUED . WATCHED" chip, in the quality you picked for the search.
'@
Write-Host 'Next: set "When found" to "Add to queue, paused" on that search BEFORE continuing.' -ForegroundColor Yellow
Read-Host 'Press Enter when done' | Out-Null
Simulate-NewVideo
Ask 'Add to queue, paused' @'
Press Check now. RECENT ACTIVITY shows a purple PAUSED line "(waiting for you in Downloads)". Downloads shows the item with a "PAUSED . WATCHED" chip,
"Found by a watched search, waiting for you", and it does not start until you press Resume.
'@
Ask 'Skipped (optional)' @'
Only if the STARTED download from "Download now" finished before the app was closed (a restart drops running downloads). Close the app, open auto_download_seen.json and delete
that video's id (printed above as "removed ..."), start the app and press Check now. Does RECENT ACTIVITY show an amber
SKIPPED line "<title>: File already exists"? Answer s to skip.
'@
Show-Log 10

# --- Optional, slow: a real new video ----------------------------------------------
Ask 'Real new video (optional, slow)' @'
Leave Twitch Leecher open with a Broadcasts search on a channel that is live now, interval 5 min.
While the stream runs: an amber WAITING line "(still live, retry next check)" appears once (not every check).
After the stream ends and the VOD is published: it is STARTED or PAUSED as chosen. Answer s to skip.
'@
Show-Log 10

if (-not $p.HasExited) { Write-Host 'Close Twitch Leecher to finish.' -ForegroundColor Yellow; $p.WaitForExit() }
$report = ($results.GetEnumerator() | ForEach-Object { '{0,-32} {1}' -f $_.Key, $_.Value }) -join [Environment]::NewLine
$file = Join-Path ([IO.Path]::GetTempPath()) "twitchleecher-ui-watched-searches-results-$(Get-Date -Format yyyyMMdd-HHmm).txt"
Set-Content -Path $file -Value $report
Write-Host ''; Write-Host '===== Results =====' -ForegroundColor Green; Write-Host $report; Write-Host "Saved to $file"
