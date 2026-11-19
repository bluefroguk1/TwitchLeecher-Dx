# Test script for the "feature-tray-notifications" branch.
# Builds the app, then walks you through the tray icon, minimise-to-tray and desktop notification checks.
# Windows:  powershell -ExecutionPolicy Bypass -File test-scripts\test-tray-notifications.ps1
# Linux:    pwsh test-scripts/test-tray-notifications.ps1   (needs ffmpeg on PATH and a notification daemon)
param([switch]$SkipBuild)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$proj = Join-Path $root (Join-Path 'TwitchLeecher' (Join-Path 'TwitchLeecher' 'TwitchLeecher.csproj'))
$out = Join-Path (Split-Path -Parent $proj) (Join-Path 'bin' (Join-Path 'Release' 'net10.0'))
$dll = Join-Path $out 'TwitchLeecher.dll'
$results = [ordered]@{}

function Start-App { Start-Process dotnet -ArgumentList "`"$dll`"" -PassThru }

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

# --- 1. Tray icon ------------------------------------------------------------
Ask 'tray icon shown' 'Is the Twitch Leecher icon in the system tray (check the ^ overflow on Windows)? Hovering shows "Twitch Leecher DX".'
Ask 'tray menu' 'Right-click the tray icon. Does the menu show "Open" and "Exit"?'

# --- 2. Minimise to tray -------------------------------------------------------
Ask 'minimise normal when off' 'In Settings, "Minimise to tray" should be UNticked by default. Minimise the window. Does it go to the taskbar as usual?'
Ask 'minimise to tray' @'
Restore the window. Settings > Application: tick "Minimise to tray" and save.
Minimise the window. Does it disappear from the taskbar while the tray icon stays?
'@
Ask 'tray click restores' 'Click (or double-click) the tray icon. Does the window come back, in front and not minimised?'
Ask 'tray Open restores' 'Minimise again, then right-click the tray icon > Open. Does the window come back?'
Ask 'close still exits' 'Click the window''s close (X) button. Does the app exit completely (tray icon gone, no process left)?'
if (-not $p.HasExited) { $p.Kill() }

$p = Start-App
Ask 'setting saved' 'The app has restarted. Is "Minimise to tray" still ticked in Settings?'
Ask 'tray Exit' 'Minimise to the tray, then right-click the tray icon > Exit. Does the app exit (tray icon gone)?'
Start-Sleep -Seconds 2
$results['process gone after Exit'] = $(if ($p.HasExited) { 'PASS' } else { 'FAIL (still running)' })
if (-not $p.HasExited) { $p.Kill() }

$p = Start-App
Ask 'window position kept' 'The app restarted. Is the window where you left it before the tray Exit (window state still saved)?'

# --- 3. Notifications ------------------------------------------------------------
Ask 'show notifications default' 'Settings > Application: is "Show notifications" ticked (it is on by default)?'
Ask 'single download toast' @'
Download ONE short VOD (Crop: Start 00:00:00, End 00:01:00).
When it reaches Done, does a desktop notification appear: "Download finished" with the video title?
(On Windows the first one may take a few seconds while the app registers itself.)
'@
Ask 'batch toast' @'
Queue THREE short VODs (crop each to 1 minute) so they run one after another.
Expected: a "Download finished" notification for each, and after the last one also "All 3 downloads finished".
'@
Ask 'no batch toast for one' 'After a single download on its own (the first check), there was NO "All N downloads finished" notification?'
Ask 'notifications off' @'
Untick "Show notifications" and save. Download one more short VOD.
Expected: it finishes with NO desktop notification. Tick the setting again afterwards.
'@
Ask 'app fine without backend' @'
Linux only (s on Windows): stop the notification daemon (e.g. kill dunst/mako) and download a short VOD.
Expected: the download still completes and the app shows no error.
'@

if (-not $p.HasExited) { Write-Host 'Close Twitch Leecher to finish.' -ForegroundColor Yellow; $p.WaitForExit() }
$report = ($results.GetEnumerator() | ForEach-Object { '{0,-40} {1}' -f $_.Key, $_.Value }) -join [Environment]::NewLine
$resultFile = Join-Path ([IO.Path]::GetTempPath()) "twitchleecher-tray-notifications-results-$(Get-Date -Format yyyyMMdd-HHmm).txt"
Set-Content -Path $resultFile -Value $report
Write-Host ''
Write-Host '===== Results =====' -ForegroundColor Green
Write-Host $report
Write-Host "Saved to $resultFile"
