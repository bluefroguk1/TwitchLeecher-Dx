# Automated check: title, channel, date and game chapters embedded in downloaded VODs. No clicking needed.
# Finds a public VOD with at least two game chapters, downloads two short audio-only crops with tl (one across a
# game change, one inside a single game), then reads each file's metadata back with the app's own ffmpeg.
# Uses tl-test-lib.ps1. Needs a Release build and the app signed in once. About a minute, a few MB.
#
#   powershell -ExecutionPolicy Bypass -File test-scripts\test-video-metadata.ps1 [-Channel twitch] [-AppDir <folder>]
#
# Exit code 0 = all checks passed, 1 = a check failed, 2 = the test could not run (see the message).
param(
    # a channel whose past broadcasts change game; Twitch's own channel usually does
    [string]$Channel = 'twitch',
    [string]$AppDir
)

. (Join-Path $PSScriptRoot 'tl-test-lib.ps1')
$work = New-Work 'metadata'

# Reads a file's metadata as ffmpeg sees it: global tags and chapters (START/END/TIMEBASE, title).
function Read-Metadata([string]$file) {
    $out = Join-Path $work ([IO.Path]::GetFileNameWithoutExtension($file) + '.meta.txt')
    & $ffmpeg -v error -y -i $file -f ffmetadata $out
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

$pick = Find-VodWithChapters $Channel
if (-not $pick) { Write-Host "CANNOT RUN: none of the last 20 broadcasts of '$Channel' has two game chapters of 2+ minutes. Try -Channel <another channel>."; exit 2 }

$vod = $pick.Video
$boundary = [int]($pick.Edges[1].node.positionMilliseconds / 1000)
$game1 = $pick.Edges[0].node.details.game.displayName
$game2 = $pick.Edges[1].node.details.game.displayName
Write-Host "VOD $($vod.id): game change at ${boundary}s ($game1 -> $game2)"

function Download([string]$name, [int]$from, [int]$to) {
    $r = Invoke-Tl @('download', $vod.id, '--quality', 'audio', '--from', "$from", '--to', "$to", '--out', $work, '--name', $name)
    if ($r.Code -ne 0) { throw "tl download exited with $($r.Code) (3 = not signed in: sign in once in the app)" }
    $file = Join-Path $work $name
    if (-not (Test-Path $file)) { throw "tl reported success but $file is missing" }
    return $file
}

try {
    # a crop across the game change: 60 s of each game
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

    # a crop inside a single game: details but no chapters
    $inside = Read-Metadata (Download 'inside.mp4' 30 90)
    Check 'single game: title tag still set' ($inside.Tags['title'] -eq $vod.title) "'$($inside.Tags['title'])'"
    Check 'single game: no chapters' ($inside.Chapters.Count -eq 0) "$($inside.Chapters.Count) chapters"
}
catch {
    Check 'test ran to the end' $false $_.Exception.Message
}

Finish $work
