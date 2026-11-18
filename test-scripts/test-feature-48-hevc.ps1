# Test script for the "feature-48-hevc" branch (#48: fetch HEVC/AV1 renditions).
# 1. Asks Twitch directly which renditions a VOD has, including ones hidden until you log in.
# 2. Walks you through checking the same list in the app and downloading an HEVC rendition.
# Windows:  powershell -ExecutionPolicy Bypass -File test-scripts\test-feature-48-hevc.ps1
# Linux:    pwsh test-scripts/test-feature-48-hevc.ps1
param([switch]$SkipBuild)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$proj = Join-Path $root (Join-Path 'TwitchLeecher' (Join-Path 'TwitchLeecher' 'TwitchLeecher.csproj'))
$out = Join-Path (Split-Path -Parent $proj) (Join-Path 'bin' (Join-Path 'Release' 'net10.0'))
$dll = Join-Path $out 'TwitchLeecher.dll'
$results = [ordered]@{}
$clientId = 'kimne78kx3ncx6brgo4mv6wki5h1ko'  # Twitch web client id, same one the app uses

function Ask($id, $text) {
    Write-Host ''
    Write-Host "[$id]" -ForegroundColor Cyan
    Write-Host $text
    do { $a = Read-Host 'Result? (y = pass, n = fail, s = skip)' } until ($a -match '^[yns]$')
    $results[$id] = @{ y = 'PASS'; n = 'FAIL'; s = 'SKIP' }[$a]
}

function Get-VodRenditions($vodId) {
    $query = 'query { videoPlaybackAccessToken(id: "' + $vodId + '", params: {platform: "web", playerBackend: "mediaplayer", playerType: "site"}) { value signature } }'
    $gql = Invoke-RestMethod -Method Post -Uri 'https://gql.twitch.tv/gql' -Headers @{ 'Client-ID' = $clientId } `
        -ContentType 'application/json' -Body (@{ query = $query } | ConvertTo-Json)
    $tok = $gql.data.videoPlaybackAccessToken
    if (-not $tok) { throw "No access token for VOD $vodId (deleted or sub-only?)" }

    $qs = 'allow_source=true&allow_audio_only=true&include_unavailable=true&platform=web&player_backend=mediaplayer' +
          '&playlist_include_framerate=true&supported_codecs=av1,h265,h264' +
          '&token=' + [uri]::EscapeDataString($tok.value) + '&sig=' + $tok.signature
    $m3u8 = (Invoke-WebRequest -UseBasicParsing -Uri "https://usher.ttvnw.net/vod/v2/$vodId.m3u8?$qs").Content
    # Windows PowerShell 5.1 returns this content type as bytes, so decode it before parsing
    if ($m3u8 -is [byte[]]) { $m3u8 = [Text.Encoding]::UTF8.GetString($m3u8) }

    Write-Host 'Available now (should match the app''s quality list):' -ForegroundColor Green
    foreach ($line in ($m3u8 -split "`n" | Where-Object { $_ -like '#EXT-X-STREAM-INF*' })) {
        $name = [regex]::Match($line, 'IVS-NAME="([^"]*)"').Groups[1].Value
        $res = [regex]::Match($line, 'RESOLUTION=([^,]*)').Groups[1].Value
        $codec = [regex]::Match($line, 'CODECS="([^",]*)').Groups[1].Value
        Write-Host ('  {0,-12} {1,-10} {2}' -f $name, $res, $codec)
    }

    $hidden = [regex]::Match($m3u8, 'unavailable-media",VALUE="([^"]+)"')
    $needsLogin = @()
    if ($hidden.Success) {
        $media = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($hidden.Groups[1].Value)) | ConvertFrom-Json
        $needsLogin = @($media | Where-Object { $_.AUTHORIZATION_REASONS -contains 'AUTHZ_NOT_LOGGED_IN' })
        if ($needsLogin.Count -gt 0) {
            Write-Host 'Hidden until logged in (should appear in the app after Sub-Only login):' -ForegroundColor Yellow
            foreach ($m in $needsLogin) { Write-Host ('  {0,-12} {1,-10} {2}' -f $m.IVS_NAME, $m.RESOLUTION, ($m.CODECS -split ',')[0]) }
        }
    }
    return $needsLogin.Count -gt 0
}

if (-not $SkipBuild) {
    dotnet build $proj -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
}
if (-not (Test-Path $dll)) { throw "App not found at $dll" }

# --- 1. What does Twitch offer for this VOD? (automatic) -------------------
Write-Host 'You need a VOD from a channel that streams in HEVC (Enhanced Broadcasting, usually 1440p).'
Write-Host 'Recent shroud VODs had a hidden 1440p60 HEVC rendition when this was written.'
$vod = Read-Host 'VOD URL or id'
$vodId = [regex]::Match($vod, '\d{6,}').Value
if (-not $vodId) { throw "No VOD id found in '$vod'" }
$hasLoginOnly = Get-VodRenditions $vodId

# --- 2. Logged out: v2 playlist still works --------------------------------
$p = Start-Process dotnet -ArgumentList "`"$dll`"" -PassThru
Ask 'Quality list matches (logged out)' @"
Make sure you are NOT logged in to Sub-Only support (Settings/Sub-Only). Search for VOD $vodId and click Download.
Does the Quality dropdown list the same renditions as 'Available now' above?
"@
Ask 'Normal download still works' 'Download it with Crop Start 00:00:00, End 00:02:00 at 1080p. Does it reach Done and play?'

# --- 3. Logged in: HEVC shows up and downloads ------------------------------
if ($hasLoginOnly) {
    Ask '#48 HEVC appears after login' @'
Log in via Sub-Only support, then open the Download dialog for the same VOD again.
Is there an entry like "1440p60 (2560x1440) HEVC" at the top of the Quality list?
'@
    $file = Read-Host 'Download that HEVC quality (crop to 2 minutes). Then paste the output file path here (blank to skip)'
    if ($file -and (Test-Path $file)) {
        $ffprobe = Get-Command ffprobe -ErrorAction SilentlyContinue
        if ($ffprobe) {
            $codec = & $ffprobe.Source -v error -select_streams v:0 -show_entries stream=codec_name,width,height -of csv=p=0 $file
            Write-Host "ffprobe: $codec"
            $results['#48 downloaded file is HEVC'] = $(if ($codec -match 'hevc') { 'PASS' } else { "FAIL ($codec)" })
        } else {
            Ask '#48 downloaded file is HEVC' 'ffprobe not found. Open the file in VLC > Tools > Codec Information. Is the video codec H.265/HEVC at 2560x1440?'
        }
    } else {
        $results['#48 downloaded file is HEVC'] = 'SKIP'
    }
} else {
    Write-Host 'This VOD has no login-only HEVC rendition, so the HEVC checks are skipped. Try another VOD.' -ForegroundColor Yellow
}

if (-not $p.HasExited) { Write-Host 'Close Twitch Leecher to finish.' -ForegroundColor Yellow; $p.WaitForExit() }
$report = ($results.GetEnumerator() | ForEach-Object { '{0,-40} {1}' -f $_.Key, $_.Value }) -join [Environment]::NewLine
$resultFile = Join-Path ([IO.Path]::GetTempPath()) "twitchleecher-hevc-results-$(Get-Date -Format yyyyMMdd-HHmm).txt"
Set-Content -Path $resultFile -Value $report
Write-Host ''
Write-Host '===== Results =====' -ForegroundColor Green
Write-Host $report
Write-Host "Saved to $resultFile"
