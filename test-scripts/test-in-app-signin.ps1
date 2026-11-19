# Test script for the "claude/in-app-signin" branch.
# Builds the app, then walks you through the in-app Twitch sign-in and sub-only setup.
# Needs: a Twitch account. Optional last step: a subscriber-only VOD of a channel you are subscribed to.
# Windows:  powershell -ExecutionPolicy Bypass -File test-scripts\test-in-app-signin.ps1
# Linux:    pwsh test-scripts/test-in-app-signin.ps1   (needs libwebkit2gtk-4.1 for the in-app window)
param([switch]$SkipBuild)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$proj = Join-Path $root (Join-Path 'TwitchLeecher' (Join-Path 'TwitchLeecher' 'TwitchLeecher.csproj'))
$out = Join-Path (Split-Path -Parent $proj) (Join-Path 'bin' (Join-Path 'Release' 'net10.0'))
$exe = Join-Path $out 'TwitchLeecher.exe'
$dll = Join-Path $out 'TwitchLeecher.dll'
$runtimeXml = Join-Path ([Environment]::GetFolderPath('ApplicationData')) (Join-Path 'Twitch Leecher DX' 'runtime.xml')
$backup = "$runtimeXml.before-signin-test"
$results = [ordered]@{}

function Start-App {
    if (Test-Path $exe) { Start-Process $exe -PassThru } else { Start-Process dotnet -ArgumentList "`"$dll`"" -PassThru }
}

function Stop-App($p) {
    if ($p -and -not $p.HasExited) {
        Write-Host 'Close Twitch Leecher to continue...' -ForegroundColor Yellow
        $p.WaitForExit()
    }
}

function Ask($id, $text) {
    Write-Host ''
    Write-Host "[$id]" -ForegroundColor Cyan
    Write-Host $text
    do { $a = Read-Host 'Result? (y = pass, n = fail, s = skip)' } until ($a -match '^[yns]$')
    $results[$id] = @{ y = 'PASS'; n = 'FAIL'; s = 'SKIP' }[$a]
}

# Checks runtime.xml: the token element exists and, on Windows, is DPAPI-protected (never plain text).
function Check-Token($id, $element, [bool]$expected) {
    $xml = if (Test-Path $runtimeXml) { [xml](Get-Content -Raw $runtimeXml) } else { $null }
    $el = if ($xml) { $xml.RuntimeData.Authentication.$element } else { $null }
    if (-not $expected) {
        $results[$id] = $(if ($el) { "FAIL ($element still stored)" } else { 'PASS' })
        return
    }
    if (-not $el) { $results[$id] = "FAIL ($element missing)"; return }
    if ($IsLinux) { $results[$id] = 'PASS (plain text on Linux, file is owner-only)'; return }
    $results[$id] = $(if ($el.Protected -eq 'DPAPI') { 'PASS' } else { 'FAIL (not protected)' })
}

# --- Build ---------------------------------------------------------------
if (-not $SkipBuild) {
    dotnet build $proj -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
}
if (-not ((Test-Path $exe) -or (Test-Path $dll))) { throw "App not found in $out" }

# Start signed out; your current sign-in is put back at the end.
if (Test-Path $runtimeXml) {
    Move-Item -Force $runtimeXml $backup
    Write-Host "Moved your runtime.xml aside to $backup (restored at the end)."
}

# --- 1. Sign in without sub-only -----------------------------------------
$p = Start-App
Ask 'Sign-in window opens in the app' @'
On the "Connect with Twitch" screen leave "Also enable sub-only downloads" UNticked and click Connect.
Expected: a "Sign in to Twitch" window opens inside the app (NOT your web browser) with the Twitch login.
Sign in. The window closes by itself and the app shows the welcome/search screen.
'@
Check-Token 'Access token stored encrypted' 'AccessToken' $true

# --- 2. Cancel ------------------------------------------------------------
Ask 'Sign-out, then cancel sign-in' @'
Click the logout button (top right) and confirm. Click Connect, then close the sign-in window without signing in.
Expected: you stay on the "Connect with Twitch" screen, no error.
'@
Check-Token 'Sign-out removed access token' 'AccessToken' $false

# --- 3. Web view forgets the session ------------------------------------
Ask 'Sign-in asks for the password again' @'
Click Connect again.
Expected: Twitch asks you to log in again (the web view did not keep you signed in). Close the window.
'@

# --- 4. Sign in with sub-only ---------------------------------------------
Ask 'Sign-in with sub-only' @'
Tick "Also enable sub-only downloads" (check the warning line under it reads sensibly) and click Connect. Sign in.
Expected: after the Twitch login the window title changes to "Enabling sub-only downloads...", then the window closes by itself.
The sub-only button in the top bar turns green.
'@
Ask 'auth-token set by OAuth login (for the handoff)' @'
Answer y if the window closed right after the OAuth login, without loading the twitch.tv home page.
Answer n if it went on to the twitch.tv home page first.
(This records which page sets the auth-token cookie.)
'@
Check-Token 'Sub-only token stored encrypted' 'AccessTokenSubOnly' $true

# --- 5. Sub-only survives a restart --------------------------------------
Stop-App $p
$p = Start-App
Ask 'Sub-only kept after restart' 'The app has restarted. Is the sub-only button still green, with no login error?'

# --- 6. Sub-only screen for users who signed in earlier ------------------
Ask 'Connect sub-only from the sub-only screen' @'
Log out, then sign in again WITHOUT ticking sub-only. Click the sub-only button in the top bar.
Expected: the screen has a "Connect sub-only" button and a collapsed "Manual method" section.
Click "Connect sub-only", sign in to Twitch in the window.
Expected: the window closes by itself, the app goes back, and the sub-only button is green.
Then open the sub-only screen once more and LEAVE IT OPEN for the next (automatic) step.
'@

# --- 7. Localhost listener needs the one-time code (automatic) -----------
try {
    $r = Invoke-WebRequest -UseBasicParsing -Uri 'http://localhost:9342/?sub_token=fake&code=wrong' -TimeoutSec 5
    $results['Listener rejects missing code'] = "FAIL (HTTP $($r.StatusCode))"
} catch {
    $code = $_.Exception.Response.StatusCode.value__
    $results['Listener rejects missing code'] = $(if ($code -eq 403) { 'PASS' } else { "FAIL ($($_.Exception.Message))" })
}
Write-Host "Listener check: $($results['Listener rejects missing code'])"

# --- 8. Optional: download a subscriber-only VOD -------------------------
Ask 'Sub-only VOD downloads (optional)' @'
Go back, search a channel you are subscribed to and download a subscriber-only VOD (crop it to 2 minutes).
Expected: the download reaches Done and the log shows "Sub-Only: Yes". Answer s if you have no such VOD.
'@

# --- Restore + summary ------------------------------------------------------
Stop-App $p
if (Test-Path $backup) {
    $a = Read-Host 'Put back your runtime.xml from before the test? (y/n)'
    if ($a -eq 'y') { Move-Item -Force $backup $runtimeXml; Write-Host 'Restored. Its token is encrypted on the next start.' }
}
$report = ($results.GetEnumerator() | ForEach-Object { '{0,-45} {1}' -f $_.Key, $_.Value }) -join [Environment]::NewLine
$file = Join-Path ([IO.Path]::GetTempPath()) "twitchleecher-in-app-signin-results-$(Get-Date -Format yyyyMMdd-HHmm).txt"
Set-Content -Path $file -Value $report
Write-Host ''
Write-Host '===== Results =====' -ForegroundColor Green
Write-Host $report
Write-Host ''
Write-Host "Saved to $file"
