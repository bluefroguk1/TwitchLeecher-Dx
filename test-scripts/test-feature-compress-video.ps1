# Test script for branch "feature-compress-video": compress the video after download.
# Windows:  powershell -ExecutionPolicy Bypass -File test-scripts\test-feature-compress-video.ps1 [-SkipBuild]
param([switch]$SkipBuild)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$proj = Join-Path $root 'TwitchLeecher\TwitchLeecher\TwitchLeecher.csproj'
$out = Join-Path (Split-Path -Parent $proj) 'bin\Release\net10.0'
$exe = Join-Path $out 'TwitchLeecher.exe'
$results = [ordered]@{}

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
Get-Process TwitchLeecher -ErrorAction SilentlyContinue | Stop-Process -Force
$p = Start-Process $exe -PassThru

Ask 'settings' @'
Settings > Download: "Compress videos after download" (off by default) and, when ticked, a Compression level
(Light / Balanced / Strong) and an Encoder (Auto / CPU / Graphics card). Tick it, choose Balanced and Auto, Save.
'@

$folder = Read-Host 'Your download folder (Settings > Download folder)'
$start = Get-Date
Ask 'download + compress' @'
Open a VOD's download dialog: "Compress after download" is ticked (it follows Settings). Crop 3 minutes, Source quality,
Download. The status goes Downloading -> Converting -> Compressing with a moving progress bar, then Done.
The log shows "Compressed X -> Y".
'@
$file = Get-ChildItem $folder -Recurse -Include *.mp4 -ErrorAction SilentlyContinue | Where-Object { $_.LastWriteTime -gt $start } | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if ($file) {
    $leftover = Get-ChildItem $folder -Recurse -Filter '*.compressing.*' -ErrorAction SilentlyContinue
    $results['no .compressing leftovers'] = $(if ($leftover) { "FAIL ($($leftover.Name -join ', '))" } else { 'PASS' })
    $codec = & (Join-Path $out 'ffmpeg.exe') -hide_banner -i $file.FullName 2>&1 | Select-String 'Video:' | Select-Object -First 1
    Write-Host "Result: $($file.Name), $([math]::Round($file.Length / 1MB)) MB, $codec"
    $results['re-encoded to hevc'] = $(if ("$codec" -match 'hevc') { 'PASS' } else { "FAIL ($codec)" })
} else { $results['file found'] = 'FAIL (no new mp4)' }

Ask 'encoder used' @'
In the download log: "Looking for a working GPU encoder... using hevc_nvenc" (or _qsv / _amf), or "none found, using the CPU".
With a GPU the Compressing step should be MUCH faster than on the CPU.
'@
Ask 'CPU vs GPU size (optional)' @'
Download the same 3 minutes once with Encoder = CPU and once with Encoder = Graphics card.
CPU should be the smaller file and slower; GPU faster and somewhat bigger. Both play fine.
'@
Ask 'GPU fallback (optional)' 'On a PC without a supported graphics card, Encoder = Graphics card still works: the log says none found, using the CPU.'
Ask 'plays' 'Open the compressed video: it plays with sound, picture is fine, and seeking works.'
Ask 'size smaller' 'Compare with the same 3 minutes downloaded with compression off: is the compressed file clearly smaller?'
Ask 'cancel' 'Start another download with compression and press Cancel while it says Compressing: it stops within seconds and no .compressing file is left.'
Ask 'untick per download' 'Untick "Compress after download" in the dialog: that download is not compressed, Settings stays ticked.'

if (-not $p.HasExited) { $p.Kill() }
$report = ($results.GetEnumerator() | ForEach-Object { '{0,-32} {1}' -f $_.Key, $_.Value }) -join [Environment]::NewLine
$file = Join-Path ([IO.Path]::GetTempPath()) "twitchleecher-compress-video-results-$(Get-Date -Format yyyyMMdd-HHmm).txt"
Set-Content -Path $file -Value $report
Write-Host ''
Write-Host '===== Results =====' -ForegroundColor Green
Write-Host $report
Write-Host "Saved to $file"
