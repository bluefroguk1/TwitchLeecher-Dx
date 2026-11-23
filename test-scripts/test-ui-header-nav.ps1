# Test script for the "ui-header-nav" branch: new header + corner toasts.
# Builds the app, then walks you through each manual check. UI only, no downloads needed.
# Windows:  powershell -ExecutionPolicy Bypass -File test-scripts\test-ui-header-nav.ps1
# Linux:    pwsh test-scripts/test-ui-header-nav.ps1
param([switch]$SkipBuild)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$proj = Join-Path $root (Join-Path 'TwitchLeecher' (Join-Path 'TwitchLeecher' 'TwitchLeecher.csproj'))
$out = Join-Path (Split-Path -Parent $proj) (Join-Path 'bin' (Join-Path 'Release' 'net11.0'))
$dll = Join-Path $out 'TwitchLeecher.dll'
$results = [ordered]@{}

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

# --- Build ---------------------------------------------------------------
if (-not $SkipBuild) {
    dotnet build $proj -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
}
if (-not (Test-Path $dll)) { throw "App not found at $dll" }

$p = Start-App
Write-Host 'App started. Log in if asked.' -ForegroundColor Cyan

# --- 1. Header layout ------------------------------------------------------
Ask 'Header layout' @'
Look at the top bar. Expected: one dark bar (about 56px tall) with the small TL logo and "Twitch Leecher" on the left,
then "Search" and "Downloads [n]" tabs, and on the right the "Settings" button (no Sub-only button any more), then icon buttons for Info, Donate (if enabled) and Log out.
No purple gradient, no "The Broadcast Downloader" subtitle.
'@

# --- 2. Active tab underline ----------------------------------------------
Ask 'Active tab underline' @'
Click "Search", then "Downloads", then "Search" again.
Expected: the tab for the page you are on has a purple underline and white text; the other is grey with no underline.
Opening Settings (or Info) removes the underline from both tabs.
'@

# --- 3. Downloads badge -----------------------------------------------------
Ask 'Downloads count badge' 'Does the grey pill next to "Downloads" show the number of downloads in the list (0 if empty)? Answer s if you cannot add one without downloading.'

# --- 4. Sub-only (moved from the header to Settings) ------------------------------------------------------
Ask 'Sub-only moved to Settings' @'
There is NO Sub-only button in the header. Open Settings: next to the "Application" heading it says
"Sub-only: off" (or "on"), and its "Set up" button opens the sub-only page.
'@

# --- 5. Info / Donate / Log out buttons ---------------------------------------
Ask 'Info, Donate, Log out buttons' @'
At the right of the header are three icon buttons: Info (?), Donate ($, only if "show donation button" is on in Settings) and Log out.
Click Info: the Info page opens. Click Log out, then answer No: you stay logged in.
'@

# --- 6. Tooltips -------------------------------------------------------------
Ask 'Tooltips' 'Hover Search, Downloads, Settings, Info, Donate and Log out. Does each show a tooltip?'

# --- 7. Toast ---------------------------------------------------------------
Ask 'Toast' @'
Open Settings and click Save.
Expected: a small dark rounded card with "Preferences saved" fades in at the bottom-right (above the Save/Cancel buttons),
stays about 5 seconds, then fades out. No purple bar across the top.
'@

Ask 'Toast restarts' @'
Click Save, wait about 3 seconds, click Save again.
Expected: the toast stays visible for a full 5 seconds after the SECOND click (it does not vanish 2s after it).
'@

Ask 'Toast does not block clicks' 'After the toast has faded, click a button that was under it (e.g. Save/Cancel). Does the click work?'

# --- 8. Theme switch --------------------------------------------------------
Ask 'Theme switch' @'
In Settings, switch the theme New -> Original (and back to New), saving each time.
Expected: the header, tab underline, Settings/Info/Donate/Log out buttons, badge and the toast all change colours with the theme,
and every label stays readable in both themes.
'@

# --- 9. Narrow window --------------------------------------------------------
Ask 'Narrow window' 'Resize the window to its minimum width. Is the header still readable, with nothing overlapping?'

# --- Summary ---------------------------------------------------------------
if (-not $p.HasExited) { Wait-AppClosed $p }
$report = ($results.GetEnumerator() | ForEach-Object { '{0,-40} {1}' -f $_.Key, $_.Value }) -join [Environment]::NewLine
$file = Join-Path ([IO.Path]::GetTempPath()) "twitchleecher-ui-header-nav-results-$(Get-Date -Format yyyyMMdd-HHmm).txt"
Set-Content -Path $file -Value $report
Write-Host ''
Write-Host '===== Results =====' -ForegroundColor Green
Write-Host $report
Write-Host ''
Write-Host "Saved to $file"
