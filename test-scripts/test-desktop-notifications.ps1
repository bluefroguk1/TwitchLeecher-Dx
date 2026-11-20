# Test script for the "feature-desktop-notifications" branch.
# Builds the app, checks the Windows toast identity (Start Menu shortcut + AUMID), then the notifications themselves.
# Windows:  powershell -ExecutionPolicy Bypass -File test-scripts\test-desktop-notifications.ps1
# Linux:    pwsh test-scripts/test-desktop-notifications.ps1   (needs ffmpeg on PATH and a notification daemon)
param([switch]$SkipBuild)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$proj = Join-Path $root (Join-Path 'TwitchLeecher' (Join-Path 'TwitchLeecher' 'TwitchLeecher.csproj'))
$out = Join-Path (Split-Path -Parent $proj) (Join-Path 'bin' (Join-Path 'Release' 'net10.0'))
$dll = Join-Path $out 'TwitchLeecher.dll'
$exe = Join-Path $out 'TwitchLeecher.exe'
$results = [ordered]@{}

# On Windows start the real exe: the Start Menu shortcut points at whatever exe started the app,
# and under "dotnet TwitchLeecher.dll" that would be dotnet.exe.
function Start-App {
    if (-not $IsLinux -and (Test-Path $exe)) { return Start-Process $exe -PassThru }
    Start-Process dotnet -ArgumentList "`"$dll`"" -PassThru
}

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

$p = Start-App
Start-Sleep -Seconds 5

# --- 1. Windows identity -----------------------------------------------------------
if (-not $IsLinux) {
    $lnk = Join-Path ([Environment]::GetFolderPath('ApplicationData')) 'Microsoft\Windows\Start Menu\Programs\Twitch Leecher DX.lnk'
    if (Test-Path $lnk) {
        $target = (New-Object -ComObject WScript.Shell).CreateShortcut($lnk).TargetPath
        Write-Host "Shortcut: $lnk -> $target"
        $results['start menu shortcut'] = "PASS ($target)"
        # the AUMID is a shell property of the .lnk; list Start Menu apps to see it
        $app = try { Get-StartApps | Where-Object { $_.AppID -eq 'TwitchLeecher.Dx' } } catch { $null }
        $results['AUMID TwitchLeecher.Dx'] = $(if ($app) { "PASS ($($app.Name))" } else { 'FAIL (not in Get-StartApps)' })
    } else {
        $results['start menu shortcut'] = "FAIL (missing $lnk)"
    }
}

# --- 2. Notifications ----------------------------------------------------------------
Ask 'show notifications default' 'Settings > Application: is "Show notifications" ticked (it is on by default)?'
Ask 'single download toast' @'
Download ONE short VOD (Crop: Start 00:00:00, End 00:01:00).
When it reaches Done, does a desktop notification appear: "Download finished" with the video title?
On Windows it should be labelled "Twitch Leecher DX". Make sure Focus assist / Do Not Disturb is OFF.
'@
Ask 'batch toast' @'
Queue THREE short VODs (crop each to 1 minute) so they run one after another.
Expected: a "Download finished" notification for each, and after the last one also "All 3 downloads finished".
'@
Ask 'no batch toast for one' 'After the single download on its own (the first check), there was NO "All N downloads finished" notification?'
Ask 'notification centre' 'Windows only (s on Linux): open the notification centre (Win+N). Are the toasts listed under "Twitch Leecher DX"?'
Ask 'notifications off' @'
Untick "Show notifications" and save. Download one more short VOD.
Expected: it finishes with NO desktop notification. Tick the setting again afterwards.
'@
Ask 'app fine without backend' @'
Linux only (s on Windows): stop the notification daemon (e.g. kill dunst/mako) and download a short VOD.
Expected: the download still completes and the app shows no error.
'@

if (-not $p.HasExited) { Write-Host 'Close Twitch Leecher to finish.' -ForegroundColor Yellow; $p.WaitForExit() }

$log = Get-ChildItem -Path ([Environment]::GetFolderPath('ApplicationData')) -Directory -Filter '*Leecher*' -ErrorAction SilentlyContinue |
    ForEach-Object { Get-ChildItem $_.FullName -Recurse -Filter '*.log' -ErrorAction SilentlyContinue } | Sort-Object LastWriteTime | Select-Object -Last 1
if ($log) { Write-Host "Newest app log (toast errors are logged there): $($log.FullName)" -ForegroundColor Yellow }

$report = ($results.GetEnumerator() | ForEach-Object { '{0,-40} {1}' -f $_.Key, $_.Value }) -join [Environment]::NewLine
$resultFile = Join-Path ([IO.Path]::GetTempPath()) "twitchleecher-desktop-notifications-results-$(Get-Date -Format yyyyMMdd-HHmm).txt"
Set-Content -Path $resultFile -Value $report
Write-Host ''
Write-Host '===== Results =====' -ForegroundColor Green
Write-Host $report
Write-Host "Saved to $resultFile"
