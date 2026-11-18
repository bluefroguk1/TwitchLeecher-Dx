# Test script for the "feature-requests" branch.
# #49: when mp4/mkv conversion fails, the unconverted .ts is kept in the download folder.
# The script swaps in a fake ffmpeg that always fails, so you can trigger the failure on demand.
# Windows:  powershell -ExecutionPolicy Bypass -File test-scripts\test-feature-requests.ps1
# Linux:    pwsh test-scripts/test-feature-requests.ps1   (needs ffmpeg on PATH)
param([switch]$SkipBuild)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$proj = Join-Path $root (Join-Path 'TwitchLeecher' (Join-Path 'TwitchLeecher' 'TwitchLeecher.csproj'))
$out = Join-Path (Split-Path -Parent $proj) (Join-Path 'bin' (Join-Path 'Release' 'net10.0'))
$dll = Join-Path $out 'TwitchLeecher.dll'
$onWindows = $env:OS -eq 'Windows_NT'
$results = [ordered]@{}

function Start-App { Start-Process dotnet -ArgumentList "`"$dll`"" -PassThru }

function Ask($id, $text) {
    Write-Host ''
    Write-Host "[$id]" -ForegroundColor Cyan
    Write-Host $text
    do { $a = Read-Host 'Result? (y = pass, n = fail, s = skip)' } until ($a -match '^[yns]$')
    $results[$id] = @{ y = 'PASS'; n = 'FAIL'; s = 'SKIP' }[$a]
}

function New-TsFiles($folder, $since) {
    Get-ChildItem -Path $folder -Filter *.ts -Recurse -ErrorAction SilentlyContinue |
        Where-Object { $_.LastWriteTime -gt $since }
}

if (-not $SkipBuild) {
    dotnet build $proj -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
}
if (-not (Test-Path $dll)) { throw "App not found at $dll" }

$folder = Read-Host 'Your Twitch Leecher download folder (Settings > Download folder)'
if (-not (Test-Path $folder)) { throw "Folder not found: $folder" }

# --- Part 1: conversion fails -> .ts is kept ------------------------------
$realFfmpeg = Join-Path $out 'ffmpeg.exe'
$backup = "$realFfmpeg.bak"
$fakeDir = Join-Path ([IO.Path]::GetTempPath()) 'tl-fake-ffmpeg'
$oldPath = $env:PATH

try {
    if ($onWindows) {
        # where.exe exits with an error on ffmpeg's arguments, which is all we need
        Copy-Item $realFfmpeg $backup -Force
        Copy-Item (Join-Path $env:SystemRoot 'System32\where.exe') $realFfmpeg -Force
    } else {
        New-Item -ItemType Directory -Force -Path $fakeDir | Out-Null
        $fake = Join-Path $fakeDir 'ffmpeg'
        Set-Content -Path $fake -Value "#!/bin/sh`necho 'fake ffmpeg failure' >&2`nexit 1"
        chmod +x $fake
        $env:PATH = "$fakeDir$([IO.Path]::PathSeparator)$oldPath"
    }

    Write-Host ''
    Write-Host '[#49] ffmpeg is now replaced with one that always fails.' -ForegroundColor Cyan
    Write-Host 'In the app: download a VOD with Crop Start 00:00:00, End 00:02:00, conversion ENABLED.'
    Write-Host 'Wait until it shows Error, then close the app.'
    $start = Get-Date
    $p = Start-App
    $p.WaitForExit()

    $kept = @(New-TsFiles $folder $start)
    if ($kept.Count -gt 0) {
        Write-Host "Found kept file(s): $($kept.FullName -join ', ')"
        $results['#49 .ts kept after failed conversion'] = 'PASS'
    } else {
        $results['#49 .ts kept after failed conversion'] = 'FAIL (no new .ts in download folder)'
    }
    Ask '#49 log message' 'Did the download log say "Conversion failed, kept unconverted video as ..." and does that .ts play (e.g. in VLC)?'
}
finally {
    # always put the real ffmpeg back
    if ($onWindows -and (Test-Path $backup)) { Move-Item $backup $realFfmpeg -Force }
    $env:PATH = $oldPath
    Remove-Item $fakeDir -Recurse -Force -ErrorAction SilentlyContinue
}

# --- Part 2: regression - normal conversion leaves no stray .ts -----------
Write-Host ''
Write-Host '[Regression] Real ffmpeg restored. Download the same short cropped VOD again, wait for Done, then close the app.' -ForegroundColor Cyan
$start = Get-Date
$p = Start-App
$p.WaitForExit()
$stray = @(New-TsFiles $folder $start)
$results['Normal download leaves no .ts'] = $(if ($stray.Count -eq 0) { 'PASS' } else { "FAIL ($($stray.FullName -join ', '))" })
Ask 'Normal download plays' 'Did that download finish as Done, and does the .mp4 play?'

# --- Summary ---------------------------------------------------------------
$report = ($results.GetEnumerator() | ForEach-Object { '{0,-40} {1}' -f $_.Key, $_.Value }) -join [Environment]::NewLine
$file = Join-Path ([IO.Path]::GetTempPath()) "twitchleecher-features-results-$(Get-Date -Format yyyyMMdd-HHmm).txt"
Set-Content -Path $file -Value $report
Write-Host ''
Write-Host '===== Results =====' -ForegroundColor Green
Write-Host $report
Write-Host ''
Write-Host "Saved to $file"
Write-Host 'You can delete the test .ts/.mp4 files from your download folder.'
