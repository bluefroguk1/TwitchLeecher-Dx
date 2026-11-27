# Test script for the "feature-start-with-windows" branch.
# Builds the app, checks the autostart entry is written/removed, the --minimized start, and a real sign-in start.
# Windows:  powershell -ExecutionPolicy Bypass -File test-scripts\test-start-with-windows.ps1
# Linux:    pwsh test-scripts/test-start-with-windows.ps1
# Run it twice: once normally, then sign out and in (or reboot) and run it with -AfterSignIn.
param([switch]$SkipBuild, [switch]$AfterSignIn)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$proj = Join-Path $root (Join-Path 'TwitchLeecher' (Join-Path 'TwitchLeecher' 'TwitchLeecher.csproj'))
$out = Join-Path (Split-Path -Parent $proj) (Join-Path 'bin' (Join-Path 'Release' 'net10.0'))
$dll = Join-Path $out 'TwitchLeecher.dll'
$results = [ordered]@{}

function Start-App([string]$extra = '') { Start-Process dotnet -ArgumentList "`"$dll`" $extra" -PassThru }

function Ask($id, $text) {
    Write-Host ''
    Write-Host "[$id]" -ForegroundColor Cyan
    Write-Host $text
    do { $a = Read-Host 'Result? (y = pass, n = fail, s = skip)' } until ($a -match '^[yns]$')
    $results[$id] = @{ y = 'PASS'; n = 'FAIL'; s = 'SKIP' }[$a]
}

function Get-AutoStart {
    if ($IsLinux) {
        $f = Join-Path $(if ($env:XDG_CONFIG_HOME) { $env:XDG_CONFIG_HOME } else { Join-Path $HOME '.config' }) 'autostart/twitchleecher.desktop'
        if (Test-Path $f) { return (Get-Content $f | Where-Object { $_ -like 'Exec=*' }) }
        return $null
    }
    return (Get-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name 'TwitchLeecher' -ErrorAction SilentlyContinue).TwitchLeecher
}

function Check($id, $expectEntry, $expectMinimized) {
    $v = Get-AutoStart
    Write-Host "Autostart entry: $v"
    $ok = ($null -ne $v) -eq $expectEntry
    if ($ok -and $expectEntry) { $ok = ($v -like '*--minimized*') -eq $expectMinimized }
    $results[$id] = $(if ($ok) { 'PASS' } else { "FAIL ($v)" })
}

if (-not $SkipBuild) {
    dotnet build $proj -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
}
if (-not (Test-Path $dll)) { throw "App not found at $dll" }

if ($AfterSignIn) {
    Ask 'starts at sign-in' 'After signing back in, was Twitch Leecher running hidden in the tray (no window, no taskbar button)?'
    Write-Host 'Exit the running copy from the tray icon (right-click > Exit), then press Enter.' -ForegroundColor Yellow
    Read-Host | Out-Null
    $p = Start-App
    Ask 'turn off' 'Untick "Start with the system" and save, then press y.'
    Check 'entry removed' $false $false
    if (-not $p.HasExited) { $p.Kill() }
} else {
    $p = Start-App
    Ask 'settings shown' 'Open Settings. Under Application, are "Start with the system" (unticked) and "Start minimised to the tray" (greyed out) there?'
    Ask 'minimised needs start' 'Tick "Start with the system". Does "Start minimised to the tray" become clickable? Leave it UNticked and save, then press y.'
    Check 'entry written' $true $false
    Ask 'set minimised' 'Tick "Start minimised to the tray" and save, then press y.'
    Check 'entry has --minimized' $true $true
    if (-not $p.HasExited) { $p.Kill() }

    # --- Hidden start --------------------------------------------------------------
    $p = Start-App '--minimized'
    Start-Sleep -Seconds 5
    Ask 'starts hidden' 'The app was started with --minimized. Is there NO window and NO taskbar button, but the tray icon is there?'
    $second = Start-App
    Start-Sleep -Seconds 5
    if (-not $second.HasExited) { $second.Kill() }
    Ask 'second launch wakes it' 'A second copy was just started. Did the hidden window open (and the second copy exit)?'
    Ask 'tray Exit after open' 'Right-click the tray icon > Exit. Did the app exit?'
    Start-Sleep -Seconds 2
    if (-not $p.HasExited) { $p.Kill() }

    $p = Start-App '--minimized'
    Start-Sleep -Seconds 5
    Ask 'tray click opens' 'Hidden start again. Click the tray icon. Does the window open at its saved size and position?'
    if (-not $p.HasExited) { $p.Kill() }

    $p = Start-App '--minimized'
    Start-Sleep -Seconds 5
    Ask 'tray exit while hidden' 'Hidden start again. Right-click the tray icon > Exit WITHOUT opening the window. Does the tray icon go away?'
    Start-Sleep -Seconds 2
    $results['process gone after hidden Exit'] = $(if ($p.HasExited) { 'PASS' } else { 'FAIL (still running)' })
    if (-not $p.HasExited) { $p.Kill() }

    Write-Host ''
    Write-Host 'Autostart is left ON. Now sign out of Windows (or your Linux desktop) and back in, or reboot,' -ForegroundColor Yellow
    Write-Host 'then run: test-scripts/test-start-with-windows.ps1 -SkipBuild -AfterSignIn' -ForegroundColor Yellow
}

$report = ($results.GetEnumerator() | ForEach-Object { '{0,-40} {1}' -f $_.Key, $_.Value }) -join [Environment]::NewLine
$resultFile = Join-Path ([IO.Path]::GetTempPath()) "twitchleecher-start-with-windows-results-$(Get-Date -Format yyyyMMdd-HHmm).txt"
Set-Content -Path $resultFile -Value $report
Write-Host ''
Write-Host '===== Results =====' -ForegroundColor Green
Write-Host $report
Write-Host "Saved to $resultFile"
