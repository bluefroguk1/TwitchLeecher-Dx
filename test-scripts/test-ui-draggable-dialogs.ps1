# Test script for the "ui-draggable-dialogs" branch: pop-up message boxes can be dragged.
# Builds the app, then walks you through each manual check. UI only, no downloads needed.
# Windows:  powershell -ExecutionPolicy Bypass -File test-scripts\test-ui-draggable-dialogs.ps1
# Linux:    pwsh test-scripts/test-ui-draggable-dialogs.ps1
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
Write-Host 'App started. Log in if asked.' -ForegroundColor Cyan

# --- 1. Drag the message box ------------------------------------------------
Ask 'Drag from the caption' @'
Open Settings and click "Defaults". A "Load default preferences?" message box opens.
Press on its bold "Defaults" caption, hold the left mouse button and move the mouse.
Expected: the message box follows the mouse and stays where you let go.
'@

Ask 'Drag from the body' 'With the same box open, drag from the empty area around the message or the icon. Does the box move?'

Ask 'Buttons still click' @'
Press the "No" button, move the mouse a little while holding it, then let go on the button.
Expected: the box does NOT move, and it closes as a normal "No" (preferences unchanged).
'@

Ask 'Right-click does not drag' 'Open Defaults again. Right-click and drag on the caption. Expected: nothing moves. Close the box with No.'

# --- 2. Keyboard ------------------------------------------------------------
Ask 'Keyboard unchanged' @'
Open Defaults again. Press Tab to move between Yes and No, and Space or Enter to press the focused one.
Expected: works as before this change. Answer with No.
'@

# --- 3. Other message boxes ---------------------------------------------------
Ask 'Logout box drags' 'Click Log out (or the logout menu item). Drag the "Do you really want to logout?" box by its caption, then answer No. Does it drag, and do you stay logged in?'

# --- 4. Linux -----------------------------------------------------------------
Ask 'Linux title bar' 'Linux only (answer s on Windows): the message box shows the normal system title bar and can be dragged by it, and also by its caption as above.'

# --- Summary ---------------------------------------------------------------
Ask 'Opens centred over the window' @'
Move the main window to a screen corner (or a second monitor) and trigger a message box again
(e.g. search a channel name that does not exist). The box opens centred over the main window, on the same screen.
'@

if (-not $p.HasExited) {
    Write-Host 'Close Twitch Leecher to continue...' -ForegroundColor Yellow
    $p.WaitForExit()
}
$report = ($results.GetEnumerator() | ForEach-Object { '{0,-40} {1}' -f $_.Key, $_.Value }) -join [Environment]::NewLine
$file = Join-Path ([IO.Path]::GetTempPath()) "twitchleecher-ui-draggable-dialogs-results-$(Get-Date -Format yyyyMMdd-HHmm).txt"
Set-Content -Path $file -Value $report
Write-Host ''
Write-Host '===== Results =====' -ForegroundColor Green
Write-Host $report
Write-Host ''
Write-Host "Saved to $file"
