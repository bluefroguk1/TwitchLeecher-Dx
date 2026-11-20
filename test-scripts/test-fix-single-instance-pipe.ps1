# Test script for the "fix-single-instance-pipe" branch.
# Builds the app, starts it, then starts it a second time and checks the first window comes forward.
# Windows:  powershell -ExecutionPolicy Bypass -File test-scripts\test-fix-single-instance-pipe.ps1
# Linux:    pwsh test-scripts/test-fix-single-instance-pipe.ps1
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

if (-not $SkipBuild) {
    dotnet build $proj -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
}
if (-not (Test-Path $dll)) { throw "App not found at $dll" }

$p = Start-App
Ask 'first instance' 'Is Twitch Leecher open? Now MINIMISE its window, then press y.'

$second = Start-App
Start-Sleep -Seconds 5
$results['second instance exits'] = $(if ($second.HasExited) { 'PASS' } else { 'FAIL (still running)' })
if (-not $second.HasExited) { $second.Kill() }
Ask 'window restored' 'Did the minimised window come back to the front (not minimised) when the second copy was started?'

Ask 'behind other window' 'Put another app''s window over Twitch Leecher (do not minimise it), then press y. The script will start a second copy again.'
$second = Start-App
Start-Sleep -Seconds 5
if (-not $second.HasExited) { $second.Kill() }
Ask 'window to front' 'Did Twitch Leecher jump in front of the other window?'

if (-not $p.HasExited) { $p.Kill() }
$report = ($results.GetEnumerator() | ForEach-Object { '{0,-40} {1}' -f $_.Key, $_.Value }) -join [Environment]::NewLine
$resultFile = Join-Path ([IO.Path]::GetTempPath()) "twitchleecher-single-instance-results-$(Get-Date -Format yyyyMMdd-HHmm).txt"
Set-Content -Path $resultFile -Value $report
Write-Host ''
Write-Host '===== Results =====' -ForegroundColor Green
Write-Host $report
Write-Host "Saved to $resultFile"
