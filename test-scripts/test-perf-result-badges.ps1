# Test script for branch "perf-result-badges".
# Windows:  powershell -ExecutionPolicy Bypass -File test-scripts\test-perf-result-badges.ps1 -SkipBuild
param([switch]$SkipBuild)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$proj = Join-Path $root 'TwitchLeecher\TwitchLeecher\TwitchLeecher.csproj'
$exe = Join-Path (Split-Path -Parent $proj) 'bin\Release\net10.0\TwitchLeecher.exe'
if (-not $SkipBuild) { dotnet build $proj -c Release; if ($LASTEXITCODE -ne 0) { throw 'Build failed' } }
Get-Process TwitchLeecher -ErrorAction SilentlyContinue | Stop-Process -Force
$p = Start-Process $exe -PassThru
$checks = @(
 'Search a channel and download one result (quick download or a 1-minute crop): its card shows QUEUED while it downloads',
 'When that download finishes, the card switches to DOWNLOADED by itself (no new search needed)',
 'With "Hide downloaded" ticked, the finished video disappears from the results by itself',
 'Tick 10 results and Download Selected: the page stays responsive and all 10 show QUEUED',
 'Change the download folder in Settings and Save: badges update (videos saved in the old folder no longer show DOWNLOADED)'
)
$results = $checks | ForEach-Object { do { $a = Read-Host "$_  [y/n/s]" } while ($a -notin 'y','n','s'); "[$a] $_" }
if (-not $p.HasExited) { $p.Kill() }
$out = Join-Path ([IO.Path]::GetTempPath()) "twitchleecher-perf-result-badges-$(Get-Date -Format yyyyMMdd-HHmm).txt"
$results | Set-Content $out; $results; Write-Host "Saved to $out"
