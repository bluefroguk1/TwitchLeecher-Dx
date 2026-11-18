# Test script for the "perf-log-append" branch.
# Windows:  powershell -ExecutionPolicy Bypass -File test-scripts\test-perf-log-append.ps1
param([switch]$SkipBuild)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$proj = Join-Path $root (Join-Path 'TwitchLeecher' (Join-Path 'TwitchLeecher' 'TwitchLeecher.csproj'))
$out = Join-Path (Split-Path -Parent $proj) (Join-Path 'bin' (Join-Path 'Release' 'net10.0'))
$dll = Join-Path $out 'TwitchLeecher.dll'
$results = [ordered]@{}

function Start-App { Start-Process dotnet -ArgumentList "`"$dll`"" -PassThru }
function Wait-AppClosed($p) { Write-Host 'Close Twitch Leecher to continue...' -ForegroundColor Yellow; $p.WaitForExit() }
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

Ask 'Live log updates' @'
Start a download of a longer VOD (a few hours, any quality), then open its log (the log icon on the download).
Expected: text keeps appearing roughly live (updates a few times per second) and the app stays responsive while it scrolls.
'@

Ask 'Final log lines present' @'
Let a short (cropped) download finish, or cancel one, with the log open.
Expected: the last lines ("Download task ended" / "was canceled" / clean-up lines) show up without needing to reopen the log.
'@

Ask 'Copy button' @'
Click Copy on the log view, then paste into a text editor.
Expected: the complete log text is pasted, identical to what the view shows.
'@

Ask 'Retry resets log' @'
Retry a failed or canceled download and open its log.
Expected: the old log is gone and only the new attempt is shown.
'@

Wait-AppClosed $p

$report = ($results.GetEnumerator() | ForEach-Object { '{0,-40} {1}' -f $_.Key, $_.Value }) -join [Environment]::NewLine
$file = Join-Path ([IO.Path]::GetTempPath()) "twitchleecher-perf-log-append-results-$(Get-Date -Format yyyyMMdd-HHmm).txt"
Set-Content -Path $file -Value $report
Write-Host ''
Write-Host '===== Results =====' -ForegroundColor Green
Write-Host $report
Write-Host "Saved to $file"
