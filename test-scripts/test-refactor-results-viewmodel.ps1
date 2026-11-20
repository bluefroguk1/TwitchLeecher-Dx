# Checklist for the results view model refactor (behaviour must be identical to combined-all).
#
#   powershell -ExecutionPolicy Bypass -File test-scripts\test-refactor-results-viewmodel.ps1
#   powershell -ExecutionPolicy Bypass -File test-scripts\test-refactor-results-viewmodel.ps1 -SkipBuild
#
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
function Section($title) { Write-Host ''; Write-Host "===== $title =====" -ForegroundColor Green }

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

Section 'Results view model refactor'

Ask 'Search a channel' 'Search a channel you know (Broadcasts, last 10). The results show on the right with the channel avatar, display name and "Broadcasts · last 10 · 10 results".'
Ask 'QUEUED badge' 'Quick-download one video (the download button on its card): its card shows QUEUED, and the badge is still there while it downloads (Downloads tab).'
Ask 'DOWNLOADED badge' 'When that download finishes, the badge changes from QUEUED to DOWNLOADED without searching again. Searching the same channel again still shows DOWNLOADED.'
Ask 'Hide downloaded' 'Turn on Hide downloaded: the DOWNLOADED cards disappear and the summary says "(N hidden)". Turn it off: they come back.'
Ask 'Select and size estimate' 'Tick two or three videos: the footer says "N selected", then after a moment adds "~X GB" (or "+ unknown"). Un-tick one: the count drops. Select all ticks every non-live video.'
Ask 'Download Selected' 'Click Download Selected: the ticked videos are queued with the default settings, a "download(s) added" message shows, and they lose their ticks and show QUEUED.'
Ask 'Quick download vs dialog' 'With Settings > Download > Quick download ON the video is queued straight away; with it OFF the download dialog opens first.'
Ask 'Channel star' 'Click the star next to the channel name: "Added to favourites", the star fills and the channel appears as a chip in the Search sidebar. Click again: "Removed from favourites". Edit favourites in Settings: the star follows.'
Ask 'Sort and list/grid' 'Change the sort (newest, oldest, longest, most viewed): the order changes. Switch between grid and list view: the same videos show, selections stay ticked.'
Ask 'Watch this search' 'Click "Watch this search": a notification says it is watching, and the watched search shows up in Settings.'

Get-Process -Id $p.Id -ErrorAction SilentlyContinue | Stop-Process -Force

$lines = foreach ($k in $results.Keys) { '{0,-32} {1}{2}' -f $k, $results[$k], $(if ($notes[$k]) { '  - ' + $notes[$k] } else { '' }) }
$report = ($lines -join [Environment]::NewLine)
$file = Join-Path ([IO.Path]::GetTempPath()) "twitchleecher-refactor-results-$(Get-Date -Format yyyyMMdd-HHmm).txt"
Set-Content -Path $file -Value $report -Encoding UTF8
Write-Host ''
Write-Host '===== Results =====' -ForegroundColor Green
Write-Host $report
Write-Host ''
Write-Host "Saved to $file - paste it to Claude." -ForegroundColor Yellow
