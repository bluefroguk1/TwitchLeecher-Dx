# Automated check: BTTV, FFZ and 7TV emotes in HTML chat. No clicking needed.
# Downloads the same audio-only crop with chat as text, html and html-emotes. Then, independently of the app,
# fetches the three providers' emote lists, counts the matching words in the text chat, and checks the HTML chat
# shows exactly that many third-party emote images, and the embedded one has them all as data (no outside links).
# Uses tl-test-lib.ps1. Needs a Release build of this branch and the app signed in once.
#
#   powershell -ExecutionPolicy Bypass -File test-scripts\test-third-party-emotes.ps1 [-Channel <a busy channel>] [-AppDir <folder>]
#
# Pick a channel whose chat uses BTTV/FFZ/7TV emotes; most big channels do. Twitch's own channel only has the
# global ones, so its chat may not use any (the test then says it can't run).
# Exit code 0 = all checks passed, 1 = a check failed, 2 = the test could not run (see the message).
param(
    [string]$Channel = 'twitch',
    [string]$AppDir,
    # crop length in minutes; longer gives more chat
    [int]$Minutes = 15
)

. (Join-Path $PSScriptRoot 'tl-test-lib.ps1')
$work = New-Work 'third-party-emotes'

$vod = Find-Vod $Channel (($Minutes + 10) * 60)
if (-not $vod) { Write-Host "CANNOT RUN: no recent broadcast of '$Channel' long enough. Try -Channel <another channel>."; exit 2 }
Write-Host "VOD $($vod.id): $($vod.title)"

# the providers' lists, fetched here without the app: global ones, then the channel's own
$codes = New-Object 'System.Collections.Generic.HashSet[string]'
function Try-Get([string]$url) { try { Invoke-RestMethod -Uri $url } catch { $null } }
$channelId = (Gql @{ query = 'query{user(login:"' + $Channel + '"){id}}' }).data.user.id
$ffz = Try-Get 'https://api.frankerfacez.com/v1/set/global'
if ($ffz) { foreach ($id in $ffz.default_sets) { foreach ($e in $ffz.sets."$id".emoticons) { [void]$codes.Add($e.name) } } }
# BTTV "modifier" entries (c!, h!, ...) are typed commands, which the app leaves as text
foreach ($e in @(Try-Get 'https://api.betterttv.net/3/cached/emotes/global')) { if ($e -and -not $e.modifier) { [void]$codes.Add($e.code) } }
foreach ($e in @((Try-Get 'https://7tv.io/v3/emote-sets/global').emotes)) { if ($e) { [void]$codes.Add($e.name) } }
if ($channelId) {
    $room = Try-Get "https://api.frankerfacez.com/v1/room/id/$channelId"
    if ($room) { foreach ($p in $room.sets.PSObject.Properties) { foreach ($e in $p.Value.emoticons) { [void]$codes.Add($e.name) } } }
    $bttv = Try-Get "https://api.betterttv.net/3/cached/users/twitch/$channelId"
    if ($bttv) { foreach ($e in @($bttv.channelEmotes) + @($bttv.sharedEmotes)) { if ($e) { [void]$codes.Add($e.code) } } }
    foreach ($e in @((Try-Get "https://7tv.io/v3/users/twitch/$channelId").emote_set.emotes)) { if ($e) { [void]$codes.Add($e.name) } }
}
Write-Host "$($codes.Count) BTTV/FFZ/7TV emote codes for this channel"

$hosts = 'cdn\.betterttv\.net|cdn\.frankerfacez\.com|cdn\.7tv\.app'
try {
    $from = 600; $to = 600 + $Minutes * 60
    $files = @{}
    foreach ($fmt in 'text', 'html', 'html-emotes') {
        $name = "emotes-$fmt.mp4"
        $d = Invoke-Tl @('download', $vod.id, '--quality', 'audio', '--from', "$from", '--to', "$to", '--out', $work, '--name', $name, '--chat', $fmt)
        Check "download --chat $fmt exits 0" ($d.Code -eq 0) "exit $($d.Code)"
        $ext = $(if ($fmt -eq 'text') { '.chat.txt' } else { '.chat.html' })
        $files[$fmt] = Get-Content -Raw -Encoding UTF8 (Join-Path $work "emotes-$fmt$ext")
    }

    # expected: words in the text chat's messages that are third-party emote codes
    $expected = 0
    foreach ($line in ($files['text'] -split "`n")) {
        if ($line -match '^\[[^\]]+\] [^:]+: (.*)$') {
            foreach ($word in $Matches[1].Trim().Split(' ')) { if ($codes.Contains($word)) { $expected++ } }
        }
    }
    if ($expected -eq 0) { Write-Host "CANNOT RUN: the sampled $Minutes minutes of chat use no BTTV/FFZ/7TV emotes. Try -Channel <a busier channel> or -Minutes 30."; exit 2 }

    # overlay (zero-width) emotes carry class="z"
    $linked = ([regex]::Matches($files['html'], "<img (class=""z"" )?src=""https://($hosts)")).Count
    # a word that is also a Twitch emote shows as the Twitch one, so allow a few fewer
    Check 'html: an image per third-party emote word' ($linked -le $expected -and $linked -ge [math]::Floor($expected * 0.9)) "$linked images, $expected emote words in the text chat"

    $outside = ([regex]::Matches($files['html-emotes'], "src=""https://($hosts)")).Count
    $embedded = ([regex]::Matches($files['html-emotes'], 'src="data:image/')).Count
    Check 'html-emotes: no outside image links' ($outside -eq 0) "$outside outside links"
    Check 'html-emotes: third-party emotes embedded' ($embedded -ge $expected) "$embedded embedded images (Twitch + third-party), at least $expected expected"
}
catch {
    Check 'test ran to the end' $false $_.Exception.Message
}

Finish $work
