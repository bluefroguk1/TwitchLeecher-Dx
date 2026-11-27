# Test script for branch "perf-downloads-idle": the Downloads page only ticks while something downloads.
# Windows:  powershell -ExecutionPolicy Bypass -File test-scripts\test-perf-downloads-idle.ps1 -SkipBuild
param([switch]$SkipBuild)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$proj = Join-Path $root 'TwitchLeecher\TwitchLeecher\TwitchLeecher.csproj'
$exe = Join-Path (Split-Path -Parent $proj) 'bin\Release\net10.0\TwitchLeecher.exe'
if (-not $SkipBuild) { dotnet build $proj -c Release; if ($LASTEXITCODE -ne 0) { throw 'Build failed' } }
Get-Process TwitchLeecher -ErrorAction SilentlyContinue | Stop-Process -Force
$p = Start-Process $exe -PassThru
Read-Host 'Sign in, open the Downloads page with nothing downloading, then press Enter (measures 20 s of CPU)'
$p.Refresh(); $t0 = $p.TotalProcessorTime; Start-Sleep 20; $p.Refresh()
$pct = ($p.TotalProcessorTime - $t0).TotalSeconds / 20 / [Environment]::ProcessorCount * 100
$idle = if ($pct -lt 0.5) { "PASS ({0:N2}% of all cores)" -f $pct } else { "FAIL ({0:N2}% of all cores)" -f $pct }
Write-Host "Idle CPU on the Downloads page: $idle"
$checks = @(
 'Start a short download: speed, time left and the summary bar update every second',
 'When it finishes, the summary bar changes to "1 done" straight away',
 'Pause / Resume and Clear finished update the page straight away'
)
$results = @("[auto] idle CPU $idle") + ($checks | ForEach-Object { do { $a = Read-Host "$_  [y/n/s]" } while ($a -notin 'y','n','s'); "[$a] $_" })
if (-not $p.HasExited) { $p.Kill() }
$out = Join-Path ([IO.Path]::GetTempPath()) "twitchleecher-perf-downloads-idle-$(Get-Date -Format yyyyMMdd-HHmm).txt"
$results | Set-Content $out; $results; Write-Host "Saved to $out"
