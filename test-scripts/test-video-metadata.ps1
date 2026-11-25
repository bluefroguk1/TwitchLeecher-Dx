# Automated check: title, channel, date and game chapters embedded in downloaded VODs. No clicking needed.
# Finds a public VOD with at least two game chapters, downloads two short audio-only crops with tl (one across a
# game change, one inside a single game), then reads each file's metadata back with the app's own ffmpeg.
#
# Needs: a Release build of final (tl.exe and ffmpeg.exe in the build output), and the app signed in once
# (tl uses its sign-in). Takes about a minute and downloads a few MB.
#
#   powershell -ExecutionPolicy Bypass -File test-scripts\test-video-metadata.ps1 [-Channel twitch] [-AppDir <folder>]
#
# Exit code 0 = all checks passed, 1 = a check failed, 2 = the test could not run (see the message).
param(
    # a channel whose past broadcasts change game; Twitch's own channel usually does
    [string]$Channel = 'twitch',
    # folder holding tl.exe and ffmpeg.exe; default: the Release build output in this checkout
    [string]$AppDir
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$work = Join-Path $env:TEMP 'tl-test\metadata'
$results = New-Object System.Collections.Generic.List[object]

function Find-Exe([string]$name) {
    $dirs = @()
    if ($AppDir) { $dirs += $AppDir }
    $dirs += Join-Path $root 'TwitchLeecher\TwitchLeecher\bin\Release'
    $dirs += Join-Path $root 'TwitchLeecher\TwitchLeecher.Cli\bin\Release'
    foreach ($d in $dirs) {
        if (Test-Path $d) {
            $f = Get-ChildItem -Path $d -Recurse -Filter $name -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1
            if ($f) { return $f.FullName }
        }
    }
    Write-Host "CANNOT RUN: $name not found. Build first (dotnet build TwitchLeecher\TwitchLeecher.sln -c Release) or pass -AppDir."
    exit 2
}

function Check([string]$name, [bool]$ok, [string]$detail) {
    $results.Add([pscustomobject]@{ Check = $name; Result = $(if ($ok) { 'PASS' } else { 'FAIL' }); Detail = $detail })
}

# the web client id the app itself uses for public Twitch requests
$constants = Get-Content -Raw (Join-Path $root 'TwitchLeecher\TwitchLeecher.Core\Constants\Constants.cs')
if ($constants -notmatch 'ClientIdWeb\s*=\s*"([^"]+)"') { Write-Host 'CANNOT RUN: ClientIdWeb not found in Constants.cs'; exit 2 }
$clientId = $Matches[1]

function Gql($body) {
    Invoke-RestMethod -Method Post -Uri 'https://gql.twitch.tv/gql' -Headers @{ 'Client-ID' = $clientId } `
        -ContentType 'text/plain' -Body ($body | ConvertTo-Json -Depth 10 -Compress)
}

# Reads a file's metadata as ffmpeg sees it: global tags and chapters (start/end in ms, title).
function Read-Metadata([string]$file) {
    $out = Join-Path $work ([IO.Path]::GetFileNameWithoutExtension($file) + '.meta.txt')
    & $script:ffmpeg -v error -y -i $file -f ffmetadata $out
    if ($LASTEXITCODE -ne 0) { throw "ffmpeg could not read the metadata of $file" }
    $meta = @{ Tags = @{}; Chapters = New-Object System.Collections.Generic.List[object] }
    $chapter = $null
    foreach ($line in Get-Content -Encoding UTF8 $out) {
        if ($line -eq '[CHAPTER]') { $chapter = @{}; $meta.Chapters.Add($chapter); continue }
        if ($line -match '^([A-Za-z_]+)=(.*)$') {
            $value = $Matches[2] -replace '\\(.)', '$1'
            if ($chapter) { $chapter[$Matches[1]] = $value } else { $meta.Tags[$Matches[1]] = $value }
        }
    }
    return $meta
}

$tl = Find-Exe 'tl.exe'
$script:ffmpeg = Find-Exe 'ffmpeg.exe'
New-Item -ItemType Directory -Force -Path $work | Out-Null
Get-ChildItem $work -File -ErrorAction SilentlyContinue | Remove-Item -Force
Write-Host "tl:     $tl"
Write-Host "ffmpeg: $script:ffmpeg"

# 1. a VOD with at least two game chapters, each at least 2 minutes long
$query = 'query{user(login:"' + $Channel + '"){videos(first:20,type:ARCHIVE){edges{node{id title lengthSeconds owner{displayName}}}}}}'
$videos = (Gql @{ query = $query }).data.user.videos.edges.node
$pick = $null
foreach ($v in $videos) {
    $r = Gql @{ operationName = 'VideoPlayer_ChapterSelectButtonVideo'; variables = @{ videoID = $v.id };
                extensions = @{ persistedQuery = @{ version = 1; sha256Hash = '71835d5ef425e154bf282453a926d99b328cdc5e32f36d3a209d0f4778b41203' } } }
    $edges = @($r.data.video.moments.edges)
    if ($edges.Count -ge 2 -and $edges[0].node.durationMilliseconds -ge 120000 -and $edges[1].node.durationMilliseconds -ge 120000) {
        $pick = @{ Video = $v; Edges = $edges }
        break
    }
}
if (-not $pick) { Write-Host "CANNOT RUN: none of the last 20 broadcasts of '$Channel' has two game chapters of 2+ minutes. Try -Channel <another channel>."; exit 2 }

$vod = $pick.Video
$boundary = [int]($pick.Edges[1].node.positionMilliseconds / 1000)
$game1 = $pick.Edges[0].node.details.game.displayName
$game2 = $pick.Edges[1].node.details.game.displayName
Write-Host "VOD $($vod.id): game change at ${boundary}s ($game1 -> $game2)"

function Download([string]$name, [int]$from, [int]$to) {
    $file = Join-Path $work $name
    & $tl download $vod.id --quality audio --from $from --to $to --out $work --name $name | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "tl download exited with $LASTEXITCODE (3 = not signed in: sign in once in the app)" }
    if (-not (Test-Path $file)) { throw "tl reported success but $file is missing" }
    return $file
}

try {
    # 2. a crop across the game change: 60 s of each game
    $across = Read-Metadata (Download 'across.mp4' ($boundary - 60) ($boundary + 60))
    Check 'title tag' ($across.Tags['title'] -eq $vod.title) "'$($across.Tags['title'])'"
    Check 'artist tag (channel)' ($across.Tags['artist'] -ieq $vod.owner.displayName) "'$($across.Tags['artist'])'"
    Check 'date tag' ($across.Tags['date'] -match '^\d{4}-\d{2}-\d{2}$') "'$($across.Tags['date'])'"
    Check 'comment tag (VOD link)' ($across.Tags['comment'] -like "*$($vod.id)*") "'$($across.Tags['comment'])'"
    $c = $across.Chapters
    Check 'two chapters across the game change' ($c.Count -eq 2) "$($c.Count) chapters"
    if ($c.Count -eq 2) {
        Check 'chapter names are the two games' ($c[0]['title'] -eq $game1 -and $c[1]['title'] -eq $game2) "'$($c[0]['title'])', '$($c[1]['title'])'"
        Check 'first chapter starts at 0' ([int64]$c[0]['START'] -eq 0) "START=$($c[0]['START'])"
        # the crop starts 60 s before the change, so the second game starts at about 60 s into the file
        $change = [int64]$c[1]['START'] * 1000 / [int64]($c[1]['TIMEBASE'] -replace '^1/', '')
        Check 'game change about 60 s in' ([math]::Abs($change - 60000) -le 2000) "second chapter starts at $([math]::Round($change / 1000, 1)) s"
    }

    # 3. a crop inside a single game: details but no chapters
    $inside = Read-Metadata (Download 'inside.mp4' 30 90)
    Check 'single game: title tag still set' ($inside.Tags['title'] -eq $vod.title) "'$($inside.Tags['title'])'"
    Check 'single game: no chapters' ($inside.Chapters.Count -eq 0) "$($inside.Chapters.Count) chapters"
}
catch {
    Check 'test ran to the end' $false $_.Exception.Message
}

$results | Format-Table -AutoSize | Out-Host
$failed = @($results | Where-Object { $_.Result -eq 'FAIL' }).Count
if ($failed -eq 0) {
    Write-Host "ALL PASSED ($($results.Count) checks). Test files deleted."
    Get-ChildItem $work -File | Remove-Item -Force
    exit 0
}
Write-Host "$failed of $($results.Count) checks FAILED. Files kept in $work for a look."
exit 1
