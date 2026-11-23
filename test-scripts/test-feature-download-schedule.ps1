# Test script for branch "feature-download-schedule": off-peak schedule for watched-search finds and paused downloads.
# Windows:  powershell -ExecutionPolicy Bypass -File test-scripts\test-feature-download-schedule.ps1 [-SkipBuild]
# Tip: set the window to start at the NEXT whole hour, so you only wait until then.
param([switch]$SkipBuild)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$proj = Join-Path $root 'TwitchLeecher\TwitchLeecher\TwitchLeecher.csproj'
$exe = Join-Path (Split-Path -Parent $proj) 'bin\Release\net11.0\TwitchLeecher.exe'
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
Get-Process TwitchLeecher -ErrorAction SilentlyContinue | Stop-Process -Force
$p = Start-Process $exe -PassThru

$next = (Get-Date).AddHours(1).ToString('HH:00')
Ask 'Settings block' @"
Settings > Download: OFF-PEAK SCHEDULE with "Only download watched-search finds during a set time", From / To hour boxes,
"Also start downloads I paused" and "Pause them again when the time is up". The sub-options are greyed out until the first is ticked.
Tick it and set From = $next, To = a few hours later. Save.
"@

Ask 'Watched find is held (outside the window)' @'
Make a watched search find something now (as in test-ui-watched-searches.ps1: remove one id from auto_download_seen.json,
restart the app, Check now) with "When found" = Download now.
RECENT ACTIVITY shows SCHEDULED "(starts at HH:00)", and Downloads shows the item as "SCHEDULED · WATCHED",
"Waits for the download schedule", not downloading.
'@

Ask 'Manual resume still works' 'Press Resume on another paused or scheduled item: it starts straight away (manual always wins).'

Ask 'Window opens (wait until the From hour)' @"
At $next (within a minute): scheduled items start by themselves. With "Also start downloads I paused" ticked, items you paused start too.
"@

Ask 'Window closes (optional, wait until the To hour)' @'
With "Pause them again when the time is up" ticked: at the To hour, what the schedule started and has not finished pauses,
shown as SCHEDULED again; downloads you started yourself are not paused. Next time the window opens they carry on.
'@

Ask 'Off = unchanged' 'Untick the schedule and Save: watched finds with "Download now" start straight away again.'

if (-not $p.HasExited) { $p.Kill() }
$report = ($results.GetEnumerator() | ForEach-Object { '{0,-44} {1}' -f $_.Key, $_.Value }) -join [Environment]::NewLine
$file = Join-Path ([IO.Path]::GetTempPath()) "twitchleecher-download-schedule-results-$(Get-Date -Format yyyyMMdd-HHmm).txt"
Set-Content -Path $file -Value $report
Write-Host ''
Write-Host '===== Results =====' -ForegroundColor Green
Write-Host $report
Write-Host "Saved to $file"
