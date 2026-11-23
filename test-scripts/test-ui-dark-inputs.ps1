# Test script for the "ui-dark-inputs" branch (dark form controls + 12/14/16/20/24 font scale).
# UI only: no downloads, no login needed.
# Windows:  powershell -ExecutionPolicy Bypass -File test-scripts\test-ui-dark-inputs.ps1
# Linux:    pwsh test-scripts/test-ui-dark-inputs.ps1
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
Start-Sleep -Seconds 3
if ($p.HasExited) { throw 'App exited on startup (likely a XAML style error). Check the console output.' }
$results['App starts'] = 'PASS'

# --- "New" theme ------------------------------------------------------------
Ask 'Settings > Theme = New' 'Open Settings and make sure Theme is "New" (save if you changed it).'

Ask 'Text boxes dark' @'
Look at the Search page and Settings. Every text box should be near-black (#16161A) with light text,
a thin grey border, rounded corners (6px) and about 36px tall.
'@
Ask 'Hover/focus border purple' 'Hover over a text box, then click into it. Does the border turn purple and stay purple while typing?'
Ask 'ComboBox dark' 'Open a dropdown (e.g. Settings > Theme or Default quality). Is the closed box dark with a purple border on hover, and the list readable?'
Ask 'Number boxes' 'Settings has number boxes (e.g. Max connections). Are they dark with ONE border (no box inside a box), and do the up/down arrows still work?'
Ask 'Date pickers' 'Search page: switch the search type to a time range. Are the From/To date pickers dark with one border, and does the calendar popup open?'
Ask 'CheckBox / RadioButton' 'Find checkboxes in Settings and the radio buttons on the Search page. Dark box/circle, purple border on hover and when checked, tick/dot visible?'
Ask 'Crop time boxes' 'Search a channel, click Download on any VOD (then cancel). Do the crop hour/min/sec boxes still fit two digits, e.g. 01:23:45?'
Ask 'Font sizes' 'Page titles (Search, Settings section headings, Download Video, Download Log) look the same weight as before, slightly smaller (20). Nothing clipped or overlapping?'
Ask 'Log text box' 'Downloads > open a log (if you have one). Is the log text still borderless on a transparent background? Answer s if no log exists.'

# --- "Original" theme + switching -----------------------------------------
Ask 'Original theme' @'
Settings > Theme = Original, Save. Inputs should be dark grey (#2A2A2A) with white text, a grey border,
and an amber (#FFC864) border on hover/focus. Re-check text boxes, dropdowns, number boxes, date pickers,
checkboxes and radio buttons.
'@
Ask 'Switch theme New <-> Original' @'
Switch the theme New -> Original -> New (Save each time) WITHOUT restarting.
After each switch, check every control again: text box, dropdown, number box, date picker, checkbox, radio button.
Do the colours follow the theme each time (no control stuck on the old theme's colours, nothing unreadable)?
'@

# --- Summary ---------------------------------------------------------------
if (-not $p.HasExited) { Wait-AppClosed $p }
$report = ($results.GetEnumerator() | ForEach-Object { '{0,-40} {1}' -f $_.Key, $_.Value }) -join [Environment]::NewLine
$file = Join-Path ([IO.Path]::GetTempPath()) "twitchleecher-ui-dark-inputs-results-$(Get-Date -Format yyyyMMdd-HHmm).txt"
Set-Content -Path $file -Value $report
Write-Host ''
Write-Host '===== Results =====' -ForegroundColor Green
Write-Host $report
Write-Host ''
Write-Host "Saved to $file"
