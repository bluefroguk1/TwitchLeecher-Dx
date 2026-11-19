# Test script for the "Coyote" theme branch.
# Builds the app, then walks you through switching to the new theme and checking every page.
# Windows:  powershell -ExecutionPolicy Bypass -File test-scripts\test-new-theme.ps1
# Linux:    pwsh test-scripts/test-new-theme.ps1
param([switch]$SkipBuild)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$proj = Join-Path $root (Join-Path 'TwitchLeecher' (Join-Path 'TwitchLeecher' 'TwitchLeecher.csproj'))
$out = Join-Path (Split-Path -Parent $proj) (Join-Path 'bin' (Join-Path 'Release' 'net10.0'))
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

# --- 1. Switch theme ------------------------------------------------------
Ask 'Theme listed + applies' @'
Open Settings (gear icon). In "Theme" pick "Coyote" and save.
Expected: the app turns dark chocolate brown with red borders straight away, with no restart needed. Text is cream.
'@

# --- 2. Main window -------------------------------------------------------
Ask 'Title bar + nav buttons' @'
Title bar: brown gradient, red line under it. Search / Downloads / small icon buttons are brown with cream text and icons.
Hover a button: it gets slightly lighter. Click and hold: lighter again. Text stays readable throughout.
'@

# --- 3. Search page --------------------------------------------------------
Ask 'Search form' @'
Go to Search. Text boxes are near-black with cream text. Clicking into one gives a turquoise border.
Select some text in a box: the highlight is teal with white text.
Checkboxes / radio buttons: visible borders, turquoise when ticked.
Open the date pickers and the drop-down lists (e.g. load limit). The open list is readable and the selected row is teal with cream text.
'@
Ask 'Search results + loading' @'
Search a channel. While loading, the spinner and "Loading" text are red and readable.
Result cards: brown gradient with red borders. Thumbnail badges (length, views, date) are still readable.
Scroll the list: the scrollbar is brown, and its arrows turn turquoise when you hover them.
'@

# --- 4. Downloads ---------------------------------------------------------
Ask 'Download dialog' 'Click Download on a VOD. The dialog (quality list, filename, crop boxes, spinner buttons) uses the same colours and every label is readable. Start a short cropped download.'
Ask 'Downloads page + progress' @'
Go to Downloads. The progress bar is turquoise on a cream track and the black % text is readable on both parts.
Open the log (log icon): cream text on a dark background.
If a download fails, the error box is a soft red with readable text.
'@
Ask 'Notification bar' 'When the download finishes (or any notification appears under the title bar), the bar is red with readable cream text.'

# --- 5. Other pages -------------------------------------------------------
Ask 'Settings, Info, sub-only, login' @'
Visit Settings (every section), Info (? icon) and the sub-only star page. Optionally log out (last icon) to see the login/welcome page, then log back in.
Expected: no white or grey panels left over, all text readable, and tooltips (hover the (?) icons) have a dark background with cream text.
'@
Ask 'Message box' 'Trigger any message box (a confirmation or error popup, e.g. closing the app while a download runs). The box has the brown gradient, not the old grey one. Answer s if you cannot trigger one.'

# --- 6. Persistence + other themes still fine -----------------------------
Wait-AppClosed $p
$p = Start-App
Ask 'Theme remembered' 'The app has restarted. Is it still in Coyote?'
Ask 'Original + New unchanged' @'
Switch to "New", then to "Original". Each looks as before (grey with a purple / gold accent).
One intended change: their progress bar is now green on light grey (their own theme colours) instead of Avalonia's blue.
Then switch back to Coyote. Every page goes fully brown again, with no grey controls left behind.
'@

# --- Summary ---------------------------------------------------------------
if (-not $p.HasExited) { Wait-AppClosed $p }
$report = ($results.GetEnumerator() | ForEach-Object { '{0,-40} {1}' -f $_.Key, $_.Value }) -join [Environment]::NewLine
$file = Join-Path ([IO.Path]::GetTempPath()) "twitchleecher-new-theme-results-$(Get-Date -Format yyyyMMdd-HHmm).txt"
Set-Content -Path $file -Value $report
Write-Host ''
Write-Host '===== Results =====' -ForegroundColor Green
Write-Host $report
Write-Host ''
Write-Host "Saved to $file"
