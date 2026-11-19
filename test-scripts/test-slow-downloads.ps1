# Test script for the "fix slow downloads" branch.
# Builds master (in a temporary git worktree) and this branch, then times the same VOD section on both.
# The chunk-download phase is timed automatically by watching the chunk files (00000001.ts, ...) in the
# download temp folder: from the first chunk byte until all chunks are on disk. ffmpeg time is not included.
# Windows:  powershell -ExecutionPolicy Bypass -File test-scripts\test-slow-downloads.ps1
#           (add -SkipBuild to reuse both builds, -Rounds 2 to alternate master/branch twice)
param([switch]$SkipBuild, [int]$Rounds = 1)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$masterRoot = Join-Path ([IO.Path]::GetTempPath()) 'tl-speedtest-master'
$results = [ordered]@{}

function Get-Dll($repo) {
    Join-Path $repo 'TwitchLeecher\TwitchLeecher\bin\Release\net10.0\TwitchLeecher.dll'
}

function Build($repo) {
    dotnet build (Join-Path $repo 'TwitchLeecher\TwitchLeecher\TwitchLeecher.csproj') -c Release
    if ($LASTEXITCODE -ne 0) { throw "Build failed in $repo" }
}

function Ask($id, $text) {
    Write-Host ''
    Write-Host "[$id]" -ForegroundColor Cyan
    Write-Host $text
    do { $a = Read-Host 'Result? (y = pass, n = fail, s = skip)' } until ($a -match '^[yns]$')
    $results[$id] = @{ y = 'PASS'; n = 'FAIL'; s = 'SKIP' }[$a]
}

# Waits for a new download folder under $tempRoot, samples the total size of its chunk files until the
# folder is cleaned up, and returns seconds and MB for "first chunk byte -> last chunk byte written".
function Measure-ChunkDownload($tempRoot) {
    $before = @(Get-ChildItem $tempRoot -Directory -ErrorAction SilentlyContinue | ForEach-Object FullName)
    $chunk = '^\d{8}\.ts$'
    Write-Host 'Start the download in the app now. Watching the temp folder...' -ForegroundColor Yellow

    $dir = $null
    while (-not $dir) {
        Start-Sleep -Milliseconds 200
        $dir = Get-ChildItem $tempRoot -Directory -ErrorAction SilentlyContinue |
            Where-Object { $before -notcontains $_.FullName -and
                (Get-ChildItem $_.FullName -File -ErrorAction SilentlyContinue | Where-Object Name -match $chunk) } |
            Select-Object -First 1
    }
    Write-Host "Found $($dir.FullName), measuring until the download finishes..."

    $sw = [Diagnostics.Stopwatch]::StartNew()
    $samples = New-Object System.Collections.Generic.List[object]
    while (Test-Path $dir.FullName) {
        $sum = (Get-ChildItem $dir.FullName -File -ErrorAction SilentlyContinue |
            Where-Object Name -match $chunk | Measure-Object Length -Sum).Sum
        $samples.Add([pscustomobject]@{ T = $sw.Elapsed.TotalSeconds; Bytes = [double]$sum })
        Start-Sleep -Milliseconds 200
    }

    # chunks are only deleted after the download phase, so the first time the peak is reached = last chunk written
    $peak = ($samples | Measure-Object Bytes -Maximum).Maximum
    $start = ($samples | Where-Object { $_.Bytes -gt 0 } | Select-Object -First 1).T
    $end = ($samples | Where-Object { $_.Bytes -eq $peak } | Select-Object -First 1).T
    $secs = [math]::Max(0.2, $end - $start)
    $mb = $peak / 1MB
    [pscustomobject]@{ Seconds = [math]::Round($secs, 1); MB = [math]::Round($mb, 1); MBps = [math]::Round($mb / $secs, 2) }
}

# --- Build ---------------------------------------------------------------
if (-not $SkipBuild) {
    if (Test-Path $masterRoot) { git -C $root worktree remove --force $masterRoot }
    git -C $root fetch origin master
    git -C $root worktree add --detach $masterRoot origin/master
    if ($LASTEXITCODE -ne 0) { throw 'Could not create the master worktree' }
    Build $masterRoot
    Build $root
}
$builds = [ordered]@{ master = (Get-Dll $masterRoot); branch = (Get-Dll $root) }
foreach ($b in $builds.GetEnumerator()) { if (-not (Test-Path $b.Value)) { throw "App not found at $($b.Value)" } }

# --- Setup -----------------------------------------------------------------
$defaultTemp = Join-Path $env:LOCALAPPDATA 'Temp'
$tempRoot = Read-Host "Download temp folder from Settings > Download [$defaultTemp]"
if (-not $tempRoot) { $tempRoot = $defaultTemp }
Write-Host @'

Pick ONE VOD and section and use it for every run:
  - same quality (Source is best for this test), e.g. Crop Start 00:10:00, End 00:30:00
  - same Max connections setting (both builds share Settings; 10 is the default)
  - same "Disable conversion" setting
A 20+ minute section gives the clearest numbers. Keep other downloads and streaming off while testing.
'@

# --- Timed runs (alternating so network drift hits both) ------------------
$times = @{ master = @(); branch = @() }
for ($r = 1; $r -le $Rounds; $r++) {
    foreach ($b in $builds.GetEnumerator()) {
        Write-Host ''
        Write-Host "[Round $r - $($b.Key)] Starting $($b.Key) build..." -ForegroundColor Cyan
        $p = Start-Process dotnet -ArgumentList "`"$($b.Value)`"" -PassThru
        $m = Measure-ChunkDownload $tempRoot
        Write-Host ("{0}: {1} MB in {2}s = {3} MB/s" -f $b.Key, $m.MB, $m.Seconds, $m.MBps) -ForegroundColor Green
        $times[$b.Key] += $m
        $results["Round $r $($b.Key)"] = "$($m.MB) MB in $($m.Seconds)s = $($m.MBps) MB/s"
        Write-Host 'Close Twitch Leecher to continue...' -ForegroundColor Yellow
        $p.WaitForExit()
    }
}

$avgMaster = ($times.master | Measure-Object MBps -Average).Average
$avgBranch = ($times.branch | Measure-Object MBps -Average).Average
$speedup = [math]::Round($avgBranch / [math]::Max(0.01, $avgMaster), 2)
$results['Average MB/s master'] = [math]::Round($avgMaster, 2)
$results['Average MB/s branch'] = [math]::Round($avgBranch, 2)
$results['Branch speed-up (x)'] = $speedup
# shortcut: fixed 1.0x threshold on a single network; repeat with -Rounds 3 if the numbers are close
$results['Branch not slower than master'] = $(if ($speedup -ge 1.0) { 'PASS' } else { "FAIL ($speedup x)" })

# --- Manual checks on the branch build -----------------------------------
$p = Start-Process dotnet -ArgumentList "`"$($builds.branch)`"" -PassThru
Ask 'Output plays' 'Open the video the branch build downloaded. Does it play from start to end of the section?'
Ask 'Cancel is quick' 'Start another download and press Cancel while it says "Downloading". Does it show Canceled within a few seconds (no retry messages in its log)?'
Ask 'Max connections = 1' 'Set Max connections to 1 and download a 2 minute section. Does it finish (slowly)? Set it back afterwards.'
if (-not $p.HasExited) { Write-Host 'Close Twitch Leecher to continue...' -ForegroundColor Yellow; $p.WaitForExit() }

# --- Summary ---------------------------------------------------------------
# the master worktree is kept so -SkipBuild works next time; remove it with: git worktree remove --force $masterRoot
$report = ($results.GetEnumerator() | ForEach-Object { '{0,-35} {1}' -f $_.Key, $_.Value }) -join [Environment]::NewLine
$file = Join-Path ([IO.Path]::GetTempPath()) "twitchleecher-slow-downloads-results-$(Get-Date -Format yyyyMMdd-HHmm).txt"
Set-Content -Path $file -Value $report
Write-Host ''
Write-Host '===== Results =====' -ForegroundColor Green
Write-Host $report
Write-Host ''
Write-Host "Saved to $file"
