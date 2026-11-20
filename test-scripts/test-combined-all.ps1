# One walk-through of everything in combined-all, quickest checks first.
#
#   powershell -ExecutionPolicy Bypass -File test-scripts\test-combined-all.ps1            (build, then Tier 1..2)
#   powershell -ExecutionPolicy Bypass -File test-scripts\test-combined-all.ps1 -SkipBuild
#   ... -Tier 3        run only Tier 3 (long downloads)      ... -Tier 4   only Tier 4 (waiting / reboot)
#
# Tier 1: instant, UI only (~25 min)   Tier 2: one short download (~20 min)
# Tier 3: long downloads (optional)    Tier 4: needs waiting, a live channel or a reboot (optional)
# Every answer is y / n / s (skip). For n you can type what went wrong. Results go to %TEMP%.
param([switch]$SkipBuild, [int]$Tier = 0)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$proj = Join-Path $root 'TwitchLeecher\TwitchLeecher\TwitchLeecher.csproj'
$out = Join-Path (Split-Path -Parent $proj) 'bin\Release\net10.0'
$exe = Join-Path $out 'TwitchLeecher.exe'
$results = [ordered]@{}
$notes = [ordered]@{}

function Ask($id, $text) {
    Write-Host ''
    Write-Host "[$id]" -ForegroundColor Cyan
    Write-Host $text
    do { $a = Read-Host 'Result? (y = pass, n = fail, s = skip)' } until ($a -match '^[yns]$')
    $results[$id] = @{ y = 'PASS'; n = 'FAIL'; s = 'SKIP' }[$a]
    if ($a -eq 'n') { $notes[$id] = Read-Host 'What went wrong? (one line)' }
}
function Section($title) { Write-Host ''; Write-Host "===== $title =====" -ForegroundColor Green }
function Run($n) { return ($Tier -eq 0 -and $n -le 2) -or $Tier -eq $n }
function Start-App {
    Get-Process TwitchLeecher -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep 1
    return Start-Process $exe -PassThru
}

# --- build ---------------------------------------------------------------------------------------------
if (-not $SkipBuild) {
    Get-Process TwitchLeecher -ErrorAction SilentlyContinue | Stop-Process -Force
    $dotnet = 'C:\Program Files\dotnet\dotnet.exe'
    if (-not (Test-Path $dotnet)) { $dotnet = 'dotnet' }
    & $dotnet build $proj -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Build failed - paste the errors to Claude' }
}
if (-not (Test-Path $exe)) { throw "App not found at $exe (build first, or unzip the GitHub artifact there)" }

$folder = Read-Host 'Your download folder (Settings > Download > Download folder)'
$p = Start-App

# =========================================================================================================
if (Run 1) {
Section 'Tier 1 - instant checks'

Start-Sleep 3
$proc = Get-Process -Id $p.Id -ErrorAction SilentlyContinue
$results['Task Manager name is TwitchLeecher (not .NET Host)'] = $(if ($proc.ProcessName -eq 'TwitchLeecher') { 'PASS' } else { "FAIL ($($proc.ProcessName))" })

Ask 'Sign in inside the app' @'
If you are signed in, log out first (Settings > General > Log out). The sign-in screen has "Also enable sub-only downloads".
Tick it and sign in: Twitch's login opens in a window INSIDE the app (no browser, no console step), and after
signing in you land on the Search page.
'@
Ask 'Opens on Search, logo + version' 'The Search page shows the app logo, "Twitch Leecher DX <version>" and "The Broadcast Downloader", plus a RECENT SEARCHES list. There is no Welcome page.'
Ask 'Header' @'
Top bar: "Twitch Leecher", Search and Downloads tabs (Downloads has a count badge, the active tab is underlined),
a Settings button and Info / Donate icon buttons, each with a tooltip. There is NO Sub-only button and NO Log out
button. Click the logo or "Twitch Leecher" from Settings or Downloads: it goes to Search.
'@
Ask 'Search sidebar' @'
Left sidebar: Channel | Links / IDs switch; a channel box with your favourites as chips under it (no star button
next to the box); type chips Broadcasts / Highlights / Uploads / Clips; a "Show" dropdown; the Search button right
under the form. There are NO Search / Cancel buttons at the bottom of the window. Enter in the channel box searches.
'@
Ask 'Date picker' @'
Show > Custom range: From and To sit level with their labels, the date text is vertically centred, the pop-up
calendar uses the theme colours (accent for the chosen day, outlined accent for today), future dates can't be
picked, and To can't be before From. Check in New and Coyote.
'@
Ask 'Text centred in boxes' 'In the sidebar and Settings, typed text sits in the vertical middle of every text box, number box and drop-down, level with labels beside them.'
Ask 'Links / IDs box' 'Switch to Links / IDs and paste or type 3 video links, pressing Enter between them: each goes on its own line, long links wrap, and Enter does NOT start a search.'
Ask 'Search tab opens the form' 'Search a channel, then click the Search tab: it opens the search form with "Back to results (N)", which takes you back.'
Ask 'Results toolbar' @'
On the results: channel name, avatar and a star (click it: added to / removed from favourites, saved),
Edit search, Hide downloaded, Sort (newest, oldest, longest, most viewed), grid/list switch; 3 cards per row with
2-line titles; LIVE / CLIP badges where they apply; Select all and "N selected" at the bottom; a "Watch this search" button.
'@
Ask 'Clips search' 'Search a channel with type Clips: clips appear with a CLIP badge and the clip creator.'
Ask 'Settings side menu' @'
Settings has a side menu: General, Search, Download, Watched searches (NO Appearance section).
General: TWITCH ACCOUNT "Signed in as <your Twitch name>" with a Log out button (click it later: back to sign-in),
THEME, Check for updates, donation button, Minimise to the system tray, Start Twitch Leecher when Windows starts,
Start minimised to the tray, Show desktop notifications. NO sub-only switch anywhere.
Search: DEFAULT CHANNEL box, NO favourite channels list. Download: folder, OFF-PEAK SCHEDULE, file name with
clickable chips and a live preview, quality, Quick download, Compress videos after download (Compression level +
Encoder), Keep the original stream file, then TEMPORARY FOLDER and MAX CONNECTIONS shown directly (NO Advanced
expander). Changing anything shows a save bar; Undo hides it again.
'@
Ask 'Themes' @'
Settings > General > Theme: switch New -> Original -> Coyote -> New, visiting Search, results, Downloads and every Settings
section each time. Nothing goes transparent or unreadable (input text, tabs, progress bars, badges, buttons).
'@
Ask 'Dialogs: centred and draggable' 'Move the window to a corner (or second monitor) and search a channel name that does not exist: the message box opens centred over the window, and can be dragged by its header.'

Write-Host ''
Write-Host 'Second launch: starting the app again while it is running...' -ForegroundColor Yellow
Start-Process $exe | Out-Null
Start-Sleep 3
Ask 'Second launch wakes the app' 'Minimise the window first if you like; the second start brings the EXISTING window to the front, and no second copy stays open.'

Ask 'Tray: first minimise asks' @'
Minimise the window. The first time, it asks "Minimise to the system tray instead?" with "Don't ask again".
Answer Yes: the window hides to the tray; clicking the tray icon brings it back, and Settings > General >
"Minimise to the system tray" is now ticked. (To ask again: set AppTrayPromptShown to false in config.xml.)
'@

Ask 'Watched searches page' @'
Settings > Watched searches: "Check watched searches for new videos" is an On/Off switch on the right. Off: everything
below it is greyed out and can't be clicked; On: it all works again. Leave it On, then add two searches. Each row is ONE line: title, "When found", dropdown, Edit, Active and
a red bin, lined up with the row above, nothing cut off (also at the smallest window width). The bin asks
"Stop watching ...?". Press Check now with a channel that has VODs: RECENT ACTIVITY shows FIRST RUN, nothing downloads.
'@

Write-Host ''
Read-Host 'Open the Downloads page with NOTHING downloading, leave it alone, then press Enter (measures 20 s of CPU)'
$proc = Get-Process TwitchLeecher | Select-Object -First 1
$t0 = $proc.TotalProcessorTime; Start-Sleep 20; $proc.Refresh()
$pct = ($proc.TotalProcessorTime - $t0).TotalSeconds / 20 / [Environment]::ProcessorCount * 100
$results['Idle CPU on Downloads page < 0.5%'] = $(if ($pct -lt 0.5) { 'PASS ({0:N2}%)' -f $pct } else { 'FAIL ({0:N2}%)' -f $pct })
Write-Host ('Idle CPU: {0:N2}% of all cores' -f $pct)
}

# =========================================================================================================
if (Run 2) {
Section 'Tier 2 - one short download'
$start = Get-Date

Ask 'Download dialog' @'
Open the download dialog for a normal VOD (not a clip): thumbnail and details at the top; Quality and Save to; the file
name is editable with a reset button; PART TO DOWNLOAD shows START/END preview frames and a filmstrip - drag the
handles and the frames and HH:MM:SS boxes follow (only digits and : can be typed); "30:00 - ~X GB" updates.
Under EXTRAS: Save chat with a format list (as text / as HTML with emotes / as HTML (works offline) / as JSON),
Save video details, Compress after download.
'@
Write-Host ''
Write-Host 'Now set: a 3-minute part, Save chat = as JSON, Save video details ticked, Compress after download ticked'
Write-Host '(Settings > Download > Compression = Balanced, Encoder = Auto), then Download.'
Ask 'Downloads page while downloading' @'
The item shows its speed and time left, the summary bar shows overall progress; Pause on the item pauses it (it stays
in the list as paused), Resume carries on from where it was. Status goes Downloading -> Converting -> Compressing ->
Done. The log names the encoder ("using hevc_nvenc/qsv/amf" or "none found, using the CPU") and "Compressed X -> Y".
'@

$new = Get-ChildItem $folder -Recurse -File -ErrorAction SilentlyContinue | Where-Object { $_.LastWriteTime -gt $start }
$json = $new | Where-Object Name -like '*.chat.json' | Select-Object -First 1
if ($json) {
    $data = Get-Content $json.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
    $prev = -1; $ordered = $true
    foreach ($m in $data.messages) { if ($m.offsetSeconds -lt $prev) { $ordered = $false }; $prev = $m.offsetSeconds }
    $count = @($data.messages).Count
    $results['Chat JSON: parses, in order'] = $(if ($count -gt 0 -and $ordered) { "PASS ($count messages)" } else { "FAIL (count $count, in order $ordered)" })
} else { $results['Chat JSON: parses, in order'] = 'FAIL (no new .chat.json)' }
$results['Video details .info.json saved'] = $(if ($new | Where-Object Name -like '*.info.json') { 'PASS' } else { 'FAIL' })
$results['No .compressing leftovers'] = $(if ($new | Where-Object Name -like '*.compressing.*') { 'FAIL' } else { 'PASS' })
$mp4 = $new | Where-Object Extension -eq '.mp4' | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if ($mp4) {
    $codec = & (Join-Path $out 'ffmpeg.exe') -hide_banner -i $mp4.FullName 2>&1 | Select-String 'Video:' | Select-Object -First 1
    $results['Compressed to HEVC'] = $(if ("$codec" -match 'hevc') { "PASS ($([math]::Round($mp4.Length/1MB)) MB)" } else { "FAIL ($codec)" })
}

Ask 'Chat as HTML' 'Download the same 3 minutes with Save chat = as HTML with emotes: open the .chat.html in a browser - names in colour, timestamps, Twitch emotes as pictures.'
Ask 'Result badges' @'
Back on the results: the video you downloaded showed QUEUED while it downloaded and now shows DOWNLOADED by itself
(no new search). With "Hide downloaded" ticked it disappears.
'@
Ask 'Download Selected' 'Tick 5 results and Download Selected: the page stays responsive, all 5 show QUEUED, then remove them from Downloads.'
Ask 'Desktop notification' 'When a download finished, a Windows notification appeared ("Download finished" + title). If not, check Focus Assist is off and say n.'
Ask 'Subscriber-only message' 'Try to download a subscriber-only VOD from a channel you are NOT subscribed to: the message is exactly "This video is for subscribers only."'
Ask 'Clip download' 'Download a clip: no crop section, no chat option, it saves as an mp4.'
}

# =========================================================================================================
if (Run 3) {
Section 'Tier 3 - long downloads (optional)'
Ask 'Chat is faster (parallel)' 'Save chat as text for 1 hour of a busy VOD: "Saving Chat" finishes clearly faster than it used to, and the lines are in time order.'
Ask 'CPU vs graphics card compression' 'Compress the same 3 minutes once with Encoder = CPU and once = Graphics card: GPU is much faster, CPU gives the smaller file, both play.'
}

# =========================================================================================================
if (Run 4) {
Section 'Tier 4 - waiting, a live channel or a reboot (optional)'
Ask 'Off-peak schedule' @'
Settings > Download > OFF-PEAK SCHEDULE: tick it, From = the next whole hour. Make a watched search find something
(Check now after removing an id from auto_download_seen.json): it appears in Downloads as SCHEDULED and waits; at the
From hour it starts by itself.
'@
Ask 'Live stream is held back' 'Watch a Broadcasts search for a channel that is live now: Check now shows WAITING (still live) and nothing downloads; after the stream ends, the next check starts that VOD.'
Ask 'Start with Windows' 'Tick "Start Twitch Leecher when Windows starts" and "Start minimised to the tray", sign out of Windows and back in: the app is running in the tray.'
Ask 'Sign-in stopped working' 'At twitch.tv > Settings > Connections, disconnect Twitch Leecher, then restart the app: it offers "Your Twitch sign-in has stopped working. Log out now and sign in again?"'
}

# --- report ----------------------------------------------------------------------------------------------
Get-Process TwitchLeecher -ErrorAction SilentlyContinue | Stop-Process -Force
$lines = foreach ($k in $results.Keys) { '{0,-48} {1}{2}' -f $k, $results[$k], $(if ($notes[$k]) { '  - ' + $notes[$k] } else { '' }) }
$report = ($lines -join [Environment]::NewLine)
$file = Join-Path ([IO.Path]::GetTempPath()) "twitchleecher-combined-all-$(Get-Date -Format yyyyMMdd-HHmm).txt"
Set-Content -Path $file -Value $report -Encoding UTF8
Write-Host ''
Write-Host '===== Results =====' -ForegroundColor Green
Write-Host $report
Write-Host ''
Write-Host "Saved to $file - paste it to Claude." -ForegroundColor Yellow
