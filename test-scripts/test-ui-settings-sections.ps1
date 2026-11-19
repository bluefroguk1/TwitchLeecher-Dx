# Test script for the "ui-settings-sections" branch (redesigned Settings page).
# UI only: no download, login or waiting needed.
# Windows:  powershell -ExecutionPolicy Bypass -File test-scripts\test-ui-settings-sections.ps1
# Linux:    pwsh test-scripts/test-ui-settings-sections.ps1
param([switch]$SkipBuild)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$proj = Join-Path $root (Join-Path 'TwitchLeecher' (Join-Path 'TwitchLeecher' 'TwitchLeecher.csproj'))
$out = Join-Path (Split-Path -Parent $proj) (Join-Path 'bin' (Join-Path 'Release' 'net10.0'))
$dll = Join-Path $out 'TwitchLeecher.dll'
$results = [ordered]@{}

$exe = Join-Path $out 'TwitchLeecher.exe'

# the exe launch gets the app's own icon and name; the dll launch is the Linux fallback
function Start-App {
    if (Test-Path $exe) { Start-Process $exe -PassThru } else { Start-Process dotnet -ArgumentList "`"$dll`"" -PassThru }
}

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

Ask 'Side menu' @'
Open Settings (gear icon). Is there a side menu on the left with General, Search, Download, Appearance,
and does clicking each one show only that section? The old Save / Undo / Default buttons in the
title bar should be gone for this page.
'@

Ask 'No save bar when clean' 'Before changing anything: is there NO bar at the bottom saying "You have unsaved changes"?'

Ask 'Download section layout' @'
Click Download. Check: download folder with a Browse button, "Put favourite channels in their own subfolder",
FILE NAME box with chips (+ Channel, + Title, + Date, + Quality, + Video ID, + Crop start) and "All wildcards...",
a Preview line, DEFAULT QUALITY, three ticks with grey sub-text (Quick download / Remove finished downloads /
Keep the original stream file - "cropping is not available"), and a collapsed "Advanced" expander.
'@

Ask 'Live preview' @'
Type in the FILE NAME box (e.g. add _test). Does the Preview line update as you type, ending in .mp4?
Now tick "Keep the original stream file": does the preview end in .ts instead?
'@

Ask 'Chips insert at the cursor' @'
Click in the middle of the file name, then click "+ Channel". Is {channel} inserted at the cursor (not at the end)?
Click "+ Date": is {year}-{month}-{day} inserted? Then open "All wildcards..." and click {hr24}: inserted too?
'@

Ask 'Save bar appears' 'After those edits, is the bottom bar showing "You have unsaved changes" with Reset to defaults, Undo and Save?'

Ask 'Undo' 'Click Undo and confirm. Do your edits revert and the bar disappear?'

Ask 'Validation shown' @'
Clear the FILE NAME box completely, switch to the General section, then click Save.
Is a red message shown in the save bar ("Please specify a default download filename!") and nothing saved?
Go back to Download: is the file name box marked as invalid? Type a name: does the red message go away?
'@

Ask 'Save' @'
Change something visible (e.g. Appearance > theme, or untick "Check for updates"), click Save.
Does a "Preferences saved" notice show, the bar disappear, and (for a theme) the theme apply?
'@

Ask 'Reset to defaults' @'
In General click "Reset all settings to defaults..." and confirm. Do the fields show defaults and the save bar appear
(nothing is saved yet)? Click Undo: are your settings back? (Do not save the defaults unless you want them.)
'@

Ask 'Advanced expander' 'In Download, expand "Advanced": temporary folder with Browse, and Max connections. Do both still work?'

Ask 'Search + General settings still work' @'
In Search, add a favourite channel with +, pick video type and load limit; in General, tick "Play videos in an external
player" and Browse to a player. Save, close and restart the app: are all of these kept?
'@

# --- Summary ---------------------------------------------------------------
if (-not $p.HasExited) { Wait-AppClosed $p }
$report = ($results.GetEnumerator() | ForEach-Object { '{0,-40} {1}' -f $_.Key, $_.Value }) -join [Environment]::NewLine
$file = Join-Path ([IO.Path]::GetTempPath()) "twitchleecher-ui-settings-sections-results-$(Get-Date -Format yyyyMMdd-HHmm).txt"
Set-Content -Path $file -Value $report
Write-Host ''
Write-Host '===== Results =====' -ForegroundColor Green
Write-Host $report
Write-Host ''
Write-Host "Saved to $file"
