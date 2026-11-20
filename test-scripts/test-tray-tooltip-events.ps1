# Tray tooltip is now refreshed on changes (max once a second) instead of by a 2-second poll.
#
#   powershell -ExecutionPolicy Bypass -File test-scripts\test-tray-tooltip-events.ps1
#   ... -SkipBuild
# Every answer is y / n / s (skip). For n you can type what went wrong. Results go to %TEMP%.
param([switch]$SkipBuild)

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

if (-not $SkipBuild) {
    Get-Process TwitchLeecher -ErrorAction SilentlyContinue | Stop-Process -Force
    $dotnet = 'C:\Program Files\dotnet\dotnet.exe'
    if (-not (Test-Path $dotnet)) { $dotnet = 'dotnet' }
    & $dotnet build $proj -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Build failed - paste the errors to Claude' }
}
if (-not (Test-Path $exe)) { throw "App not found at $exe (build first)" }

Get-Process TwitchLeecher -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep 1
$p = Start-Process $exe -PassThru

Ask 'Idle tooltip' 'Nothing downloading or paused, no watched searches (or watching switched off): hover the tray icon. It says just "Twitch Leecher DX".'
Ask 'Watched searches' 'Add a watched search and enable watching (Settings): within a second the tooltip says "1 watched search" (or "N watched searches"). Switch watching off: it goes back to "Twitch Leecher DX".'
Ask 'Download progress' 'Start a download and hover the tray icon now and then: "Twitch Leecher DX · Downloading · NN% left" with the % falling as it goes (about once a second at most). Start a second one: "2 downloading · NN% left".'
Ask 'Paused' 'Pause the download(s): the tooltip says "1 paused download" (or "N paused downloads") within a second or two.'
Ask 'Back to idle' 'Cancel or finish everything: the tooltip returns to the watched-search count or plain "Twitch Leecher DX".'
Ask 'Idle CPU' 'With nothing downloading, Task Manager shows TwitchLeecher at 0% CPU (the old 2-second poll is gone).'

Get-Process TwitchLeecher -ErrorAction SilentlyContinue | Stop-Process -Force
$lines = foreach ($k in $results.Keys) { '{0,-30} {1}{2}' -f $k, $results[$k], $(if ($notes[$k]) { '  - ' + $notes[$k] } else { '' }) }
$report = ($lines -join [Environment]::NewLine)
$file = Join-Path ([IO.Path]::GetTempPath()) "twitchleecher-tray-tooltip-$(Get-Date -Format yyyyMMdd-HHmm).txt"
Set-Content -Path $file -Value $report -Encoding UTF8
Write-Host ''
Write-Host '===== Results =====' -ForegroundColor Green
Write-Host $report
Write-Host "Saved to $file - paste it to Claude." -ForegroundColor Yellow
