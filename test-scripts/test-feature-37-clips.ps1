# Test script for the "feature-37-clips" branch (#37: search and download clips).
# Windows:  powershell -ExecutionPolicy Bypass -File test-scripts\test-feature-37-clips.ps1
# Linux:    pwsh test-scripts/test-feature-37-clips.ps1
param([switch]$SkipBuild)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$proj = Join-Path $root (Join-Path 'TwitchLeecher' (Join-Path 'TwitchLeecher' 'TwitchLeecher.csproj'))
$out = Join-Path (Split-Path -Parent $proj) (Join-Path 'bin' (Join-Path 'Release' 'net10.0'))
$dll = Join-Path $out 'TwitchLeecher.dll'
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
if (-not (Test-Path $dll)) { throw "App not found at $dll" }

$folder = Read-Host 'Your Twitch Leecher download folder (Settings > Download folder)'
$start = Get-Date
$p = Start-Process dotnet -ArgumentList "`"$dll`"" -PassThru

# --- Search -----------------------------------------------------------------
Ask '#37 channel clips, custom date range' @'
Search > Channel: pick a popular channel, Video Type = Clip, Load Limit = Timespan,
set From to about 6 months ago and To = today (a range Twitch's own site does not offer).
Expected: clips appear with thumbnails, titles, view counts, lengths, all created inside that range.
'@
Ask '#37 channel clips, last N' 'Same channel, Load Limit = last 10. Expected: exactly 10 clips (Twitch returns the most viewed first).'
Ask '#37 favourites clips' 'Favourites search with Video Type = Clip (needs a favourite channel). Expected: clips from your favourites.'
Ask '#37 clip URLs' @'
Search > URLs: paste both styles on separate lines, e.g.
  https://www.twitch.tv/<channel>/clip/<SlugName>
  https://clips.twitch.tv/<SlugName>
plus one normal VOD URL. Expected: both clips and the VOD are listed (no duplicates if the same clip is pasted twice).
'@
Ask '#37 play button' 'Click a clip thumbnail. Expected: the clip opens in your browser / external player.'

# --- Download ---------------------------------------------------------------
Ask '#37 download dialog' @'
Click download on a clip. Expected: qualities like "1080p60 (1920x1080)", 720p..., the filename ends in .mp4,
and there is NO crop section (clips are downloaded as-is).
'@
Ask '#37 download finishes' 'Click Download. Expected: Done within seconds, and the file plays with sound.'
Ask '#37 default quality' 'In Settings set Default Quality to 720p, then open the download dialog on a clip. Is 720p pre-selected? (Set it back afterwards.)'
Ask '#37 disable conversion setting' 'Tick "Disable conversion" in Settings, then download a clip. Is it still saved as a playable .mp4 (not .ts)? Untick it afterwards.'

if (-not $p.HasExited) { Write-Host 'Close Twitch Leecher to finish.' -ForegroundColor Yellow; $p.WaitForExit() }

# --- Automatic: what landed on disk ------------------------------------------
if ($folder -and (Test-Path $folder)) {
    $new = @(Get-ChildItem $folder -Recurse -File -ErrorAction SilentlyContinue | Where-Object { $_.LastWriteTime -gt $start })
    $parts = @($new | Where-Object { $_.Name -like '*.part' })
    $mp4 = @($new | Where-Object { $_.Extension -eq '.mp4' })
    Write-Host "New .mp4 files: $($mp4.Count)"
    $results['Clip .mp4 files created'] = $(if ($mp4.Count -gt 0) { 'PASS' } else { 'FAIL (none found)' })
    $results['No leftover .part files'] = $(if ($parts.Count -eq 0) { 'PASS' } else { "FAIL ($($parts.Name -join ', '))" })
    $ffprobe = Get-Command ffprobe -ErrorAction SilentlyContinue
    if ($ffprobe -and $mp4.Count -gt 0) {
        $bad = @($mp4 | Where-Object { -not (& $ffprobe.Source -v error -show_entries format=duration -of csv=p=0 $_.FullName) })
        $results['Clips readable by ffprobe'] = $(if ($bad.Count -eq 0) { 'PASS' } else { "FAIL ($($bad.Name -join ', '))" })
    }
}

$report = ($results.GetEnumerator() | ForEach-Object { '{0,-40} {1}' -f $_.Key, $_.Value }) -join [Environment]::NewLine
$resultFile = Join-Path ([IO.Path]::GetTempPath()) "twitchleecher-clips-results-$(Get-Date -Format yyyyMMdd-HHmm).txt"
Set-Content -Path $resultFile -Value $report
Write-Host ''
Write-Host '===== Results =====' -ForegroundColor Green
Write-Host $report
Write-Host "Saved to $resultFile"
