# Automated check: chat badges, overlay (zero-width) emotes, cheermotes, and the two chat settings. No clicking needed.
# Downloads the same audio-only crop's chat as html and json with "Saved chat: include badges" on and off, and
# "Saved chat: high-resolution emotes" off and on, switching them in config.xml (copied first, always put back).
# Overlay emotes and cheermotes are checked when the sampled chat has any; otherwise the script notes it.
# Uses tl-test-lib.ps1. Needs a Release build of this branch and the app signed in once (and closed).
#
#   powershell -ExecutionPolicy Bypass -File test-scripts\test-chat-extras.ps1 -Channel <a busy channel> [-Minutes 15] [-AppDir <folder>]
#
# Exit code 0 = all checks passed, 1 = a check failed, 2 = the test could not run (see the message).
param(
    [string]$Channel = 'twitch',
    [int]$Minutes = 15,
    [string]$AppDir
)

. (Join-Path $PSScriptRoot 'tl-test-lib.ps1')
$work = New-Work 'chat-extras'
$config = Join-Path $env:APPDATA 'Twitch Leecher DX\config.xml'
if (-not (Test-Path $config)) { Write-Host "CANNOT RUN: $config not found. Start the app and save Settings once."; exit 2 }
if (Get-Process TwitchLeecher -ErrorAction SilentlyContinue) { Write-Host 'CANNOT RUN: close Twitch Leecher first (it would overwrite the settings).'; exit 2 }
Copy-Item $config (Join-Path $work 'config.backup.xml') -Force

function Set-Chat([bool]$badges, [bool]$highRes) {
    [xml]$x = Get-Content -Raw -Encoding UTF8 $config
    $download = $x.SelectSingleNode('//Download')
    foreach ($pair in @(@('ChatBadges', $badges), @('ChatHighResEmotes', $highRes))) {
        $el = $download.SelectSingleNode($pair[0])
        if (-not $el) { $el = $x.CreateElement($pair[0]); [void]$download.AppendChild($el) }
        $el.InnerText = $(if ($pair[1]) { 'True' } else { 'False' })
    }
    $x.Save($config)
}

$vod = Find-Vod $Channel (($Minutes + 10) * 60)
if (-not $vod) { Write-Host "CANNOT RUN: no recent broadcast of '$Channel' long enough. Try -Channel <another channel>."; exit 2 }
Write-Host "VOD $($vod.id): $($vod.title)"
$from = 600; $to = 600 + $Minutes * 60

# Downloads the crop's chat in one format with the given settings; returns the chat file's text.
function Get-Chat([string]$tag, [string]$fmt, [bool]$badges, [bool]$highRes) {
    Set-Chat $badges $highRes
    $name = "$tag.mp4"
    $d = Invoke-Tl @('download', $vod.id, '--quality', 'audio', '--from', "$from", '--to', "$to", '--out', $work, '--name', $name, '--chat', $fmt)
    if ($d.Code -ne 0) { throw "tl download ($tag) exited with $($d.Code)" }
    $ext = $(if ($fmt -eq 'json') { '.chat.json' } elseif ($fmt -eq 'text') { '.chat.txt' } else { '.chat.html' })
    return Get-Content -Raw -Encoding UTF8 (Join-Path $work "$tag$ext")
}

try {
    $jsonOn = (Get-Chat 'json-badges' 'json' $true $false) | ConvertFrom-Json
    $withBadges = @($jsonOn.messages | Where-Object { @($_.badges).Count -gt 0 }).Count
    if (@($jsonOn.messages).Count -eq 0) { Write-Host 'CANNOT RUN: no chat in the sampled minutes. Try -Channel <a busier channel>.'; exit 2 }

    # badges
    $htmlOn = Get-Chat 'html-badges' 'html' $true $false
    $badgeImgs = ([regex]::Matches($htmlOn, '<img class="g"')).Count
    Check 'json (badges on): messages carry their badges' ($withBadges -gt 0) "$withBadges of $(@($jsonOn.messages).Count) messages have badges"
    Check 'html (badges on): badge images before names' ($badgeImgs -ge $withBadges * 0.9 -and $badgeImgs -gt 0) "$badgeImgs badge images"
    $htmlOff = Get-Chat 'html-nobadges' 'html' $false $false
    $jsonOff = (Get-Chat 'json-nobadges' 'json' $false $false) | ConvertFrom-Json
    Check 'html (badges off): no badge images' (([regex]::Matches($htmlOff, '<img class="g"')).Count -eq 0) 'none expected'
    Check 'json (badges off): no badges field' (@($jsonOff.messages | Where-Object { $null -ne $_.badges }).Count -eq 0) 'none expected'

    # high-resolution images
    $twitchLow = ([regex]::Matches($htmlOn, 'emoticons/v2/[^"]+/2\.0"')).Count
    $htmlHi = Get-Chat 'html-hires' 'html' $true $true
    $twitchHi = ([regex]::Matches($htmlHi, 'emoticons/v2/[^"]+/3\.0"')).Count
    $badgesHiOk = -not ($htmlHi -match 'badges/v1/[^"]+/2"')
    if ($twitchLow -gt 0) {
        Check 'high-res off: Twitch emotes at 2.0' ($twitchLow -gt 0 -and -not ($htmlOn -match '/3\.0"')) "$twitchLow at 2.0"
        Check 'high-res on: Twitch emotes at 3.0' ([math]::Abs($twitchHi - $twitchLow) -le [math]::Ceiling($twitchLow * 0.05) -and -not ($htmlHi -match 'emoticons/v2/[^"]+/2\.0"')) "$twitchHi at 3.0"
    }
    else { Write-Host 'NOTE: no Twitch emotes in this chat sample, so the emote size check was skipped.' }
    Check 'high-res on: badges at the largest size' $badgesHiOk 'no 2x badge links'
    Check 'high-res on: third-party emotes at the largest size' (-not ($htmlHi -match 'cdn\.betterttv\.net/emote/[^"]+/2x"|cdn\.7tv\.app/emote/[^"]+/2x\.webp"')) 'no 2x BTTV/7TV links'

    # overlay emotes: each sits inside the box of the emote before it
    $overlays = ([regex]::Matches($htmlOn, '<img class="z"')).Count
    $nested = ([regex]::Matches($htmlOn, '<span class="e"><img [^>]*>(<img class="z" [^>]*>)+</span>')).Count
    if ($overlays -gt 0) { Check 'overlay emotes sit on the emote before them' ($nested -gt 0 -and ([regex]::Matches($htmlOn, '<span class="e"><img [^>]*>((<img class="z" [^>]*>)+)</span>') | ForEach-Object { ([regex]::Matches($_.Groups[1].Value, '<img')).Count } | Measure-Object -Sum).Sum -eq $overlays) "$overlays overlay images in $nested emote boxes" }
    else { Write-Host 'NOTE: no overlay (zero-width) emotes in this chat sample.' }

    # cheermotes: words like Cheer100 become an animated gem and the amount
    $cheerImgs = ([regex]::Matches($htmlOn, 'cloudfront\.net/actions/')).Count
    if ($cheerImgs -gt 0) { Check 'cheermotes shown with their amount' (([regex]::Matches($htmlOn, 'cloudfront\.net/actions/[^>]+><span class="b"')).Count -eq $cheerImgs) "$cheerImgs cheermotes" }
    else { Write-Host 'NOTE: no cheers (bits) in this chat sample, so cheermotes were not checked.' }

    # embedded: nothing linked from outside
    $embedded = Get-Chat 'html-emotes' 'html-emotes' $true $false
    Check 'embedded chat has no outside image links' (-not ($embedded -match 'src="https?://')) "$(([regex]::Matches($embedded, 'src="data:image/')).Count) embedded images"
}
catch {
    Check 'test ran to the end' $false $_.Exception.Message
}
finally {
    Copy-Item (Join-Path $work 'config.backup.xml') $config -Force
    Write-Host 'Settings file put back as it was.'
}

Finish $work
