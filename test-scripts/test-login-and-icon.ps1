# Test script for the "login page and icon" changes.
# Builds the app, launches the real exe (so Task Manager shows "Twitch Leecher", not ".NET Host"), then walks you through each check.
# Windows:  powershell -ExecutionPolicy Bypass -File test-scripts\test-login-and-icon.ps1
# Linux:    pwsh test-scripts/test-login-and-icon.ps1
param([switch]$SkipBuild)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$proj = Join-Path $root (Join-Path 'TwitchLeecher' (Join-Path 'TwitchLeecher' 'TwitchLeecher.csproj'))
$out = Join-Path (Split-Path -Parent $proj) (Join-Path 'bin' (Join-Path 'Release' 'net11.0'))
$exe = Join-Path $out 'TwitchLeecher.exe'
$dll = Join-Path $out 'TwitchLeecher.dll'
$results = [ordered]@{}

function Start-App {
    # the apphost exe carries the app icon and name; dotnet <dll> shows up as ".NET Host"
    if (Test-Path $exe) { Start-Process $exe -PassThru }
    else { Start-Process dotnet -ArgumentList "`"$dll`"" -PassThru }
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
if (-not (Test-Path $exe) -and -not (Test-Path $dll)) { throw "App not found in $out" }
if (-not (Test-Path $exe)) { Write-Host "No TwitchLeecher.exe (non-Windows?), falling back to dotnet $dll" -ForegroundColor Yellow }

# --- 1. Icon and process name ---------------------------------------------
$p = Start-App
Start-Sleep -Seconds 5
$p.Refresh()
Write-Host ''
Write-Host "Process name: $($p.ProcessName)"
$results['Process is TwitchLeecher (auto)'] = $(if ($p.ProcessName -eq 'TwitchLeecher') { 'PASS' } else { "FAIL ($($p.ProcessName))" })

Ask 'Window + taskbar icon' 'Does the window title bar AND the taskbar button show the Twitch Leecher logo (not the generic .NET icon)?'
Ask 'Task Manager name' 'Open Task Manager > Processes. Is it listed as "Twitch Leecher" / TwitchLeecher.exe with the logo (not ".NET Host")?'
Ask 'Exe icon in Explorer' "Open $out in Explorer. Does TwitchLeecher.exe show the logo?"

# --- 2. Login callback page + bring to front --------------------------------
Write-Host ''
Write-Host 'If you are already signed in, use the Logout/revoke button in the top bar first so the Twitch login runs again.' -ForegroundColor Yellow
Ask 'Login page' @'
Click Connect and finish the Twitch login in your browser.
Expected: the browser shows a dark page with a purple bar: "Signed in to Twitch Leecher" / "You can close this tab"
(no plain text, no "Token recieved" typo). It is fine if the tab does not close by itself.
'@
Ask 'App comes to front after login' 'Right after the login page appeared, did Twitch Leecher jump in front of the browser (and un-minimise if it was minimised)?'

Ask 'Sub-only login (optional)' @'
Go to the sub-only login page and sign in there (minimise the app first).
Expected: the same "Signed in" page, and the app comes back to the front. Answer s to skip.
'@

Ask 'Second instance still activates' 'Minimise the app, then start TwitchLeecher.exe again from Explorer. Does the existing window come back to the front?'

# --- Summary ---------------------------------------------------------------
if (-not $p.HasExited) { Wait-AppClosed $p }
$report = ($results.GetEnumerator() | ForEach-Object { '{0,-40} {1}' -f $_.Key, $_.Value }) -join [Environment]::NewLine
$file = Join-Path ([IO.Path]::GetTempPath()) "twitchleecher-login-icon-results-$(Get-Date -Format yyyyMMdd-HHmm).txt"
Set-Content -Path $file -Value $report
Write-Host ''
Write-Host '===== Results =====' -ForegroundColor Green
Write-Host $report
Write-Host ''
Write-Host "Saved to $file"
