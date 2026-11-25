# Automated check: `tl info` (JSON) and `tl download --chat` (text, json, html, html-emotes). No clicking needed.
# Uses tl-test-lib.ps1. Needs a Release build and the app signed in once. A few minutes, about 20 MB.
#
#   powershell -ExecutionPolicy Bypass -File test-scripts\test-tl-info-chat.ps1 [-Channel twitch] [-AppDir <folder>]
#
# Exit code 0 = all checks passed, 1 = a check failed, 2 = the test could not run (see the message).
param(
    [string]$Channel = 'twitch',
    [string]$AppDir
)

. (Join-Path $PSScriptRoot 'tl-test-lib.ps1')
$work = New-Work 'info-chat'

$pick = Find-VodWithChapters $Channel
if (-not $pick) { Write-Host "CANNOT RUN: no recent broadcast of '$Channel' with two game chapters. Try -Channel <another channel>."; exit 2 }
$vod = $pick.Video
Write-Host "VOD $($vod.id): $($vod.title)"

try {
    # tl info
    $r = Invoke-Tl @('info', $vod.id)
    Check 'tl info exits 0' ($r.Code -eq 0) "exit $($r.Code)"
    $info = $r.Out | ConvertFrom-Json
    Check 'info: id and title' ($info.id -eq $vod.id -and $info.title -eq $vod.title) "id $($info.id), '$($info.title)'"
    Check 'info: channel' ($info.channel -ieq $vod.owner.displayName) "'$($info.channel)'"
    Check 'info: length' ([math]::Abs($info.lengthSeconds - $vod.lengthSeconds) -le 2) "$($info.lengthSeconds) s (Twitch says $($vod.lengthSeconds))"
    Check 'info: qualities listed' (@($info.qualities).Count -ge 1) "$(@($info.qualities) -join ', ')"
    Check 'info: game chapters' (@($info.chapters).Count -eq $pick.Edges.Count) "$(@($info.chapters).Count) chapters, Twitch has $($pick.Edges.Count)"
    Check 'info: recorded date is a date' ($info.recordedDate -as [datetime]) "'$($info.recordedDate)'"

    $bad = Invoke-Tl @('info', 'not-a-vod')
    Check 'info with a bad id exits 2' ($bad.Code -eq 2) "exit $($bad.Code)"
    $badChat = Invoke-Tl @('download', $vod.id, '--chat', 'pdf')
    Check 'download --chat pdf exits 2' ($badChat.Code -eq 2) "exit $($badChat.Code)"

    # one 5-minute audio-only crop per chat format, from the middle of the first chapter
    $from = 600; $to = 900
    foreach ($f in @(@{ Fmt = 'text'; Ext = '.chat.txt' }, @{ Fmt = 'json'; Ext = '.chat.json' },
                     @{ Fmt = 'html'; Ext = '.chat.html' }, @{ Fmt = 'html-emotes'; Ext = '.chat.html' })) {
        $name = "chat-$($f.Fmt).mp4"
        $d = Invoke-Tl @('download', $vod.id, '--quality', 'audio', '--from', "$from", '--to', "$to", '--out', $work, '--name', $name, '--chat', $f.Fmt)
        Check "download --chat $($f.Fmt) exits 0" ($d.Code -eq 0) "exit $($d.Code)"
        $chat = Join-Path $work ([IO.Path]::GetFileNameWithoutExtension($name) + $f.Ext)
        $exists = Test-Path $chat
        Check "$($f.Fmt): chat file saved" $exists "$([IO.Path]::GetFileName($chat))"
        if (-not $exists) { continue }
        $text = Get-Content -Raw -Encoding UTF8 $chat
        switch ($f.Fmt) {
            'text' {
                $lines = @(Get-Content -Encoding UTF8 $chat | Where-Object { $_ })
                $bad = @($lines | Where-Object { $_ -notmatch '^\[\d{1,2}:\d{2}:\d{2}\] [^:]+: ' })
                Check 'text: every line is [h:mm:ss] name: message' ($bad.Count -eq 0) "$($lines.Count) lines, $($bad.Count) malformed"
                $script:textCount = $lines.Count
            }
            'json' {
                $json = $text | ConvertFrom-Json
                Check 'json: video id and messages' ($json.video.id -eq $vod.id -and $null -ne $json.messages) "$(@($json.messages).Count) messages"
                Check 'json: same number of messages as text' (@($json.messages).Count -eq $script:textCount) "json $(@($json.messages).Count), text $script:textCount"
            }
            'html' { Check 'html: a web page with the messages' ($text -match '<html' -and $text -match '</html>') "$($text.Length) characters" }
            'html-emotes' {
                $img = ([regex]::Matches($text, '<img')).Count
                $embedded = ([regex]::Matches($text, 'src="data:image/')).Count
                Check 'html-emotes: emotes embedded as data' ($img -eq $embedded) "$img images, $embedded embedded"
            }
        }
    }
    if ($script:textCount -eq 0) { Write-Host 'NOTE: the sampled 5 minutes had no chat messages, so the chat checks were thin. Try -Channel <a busier channel>.' }
}
catch {
    Check 'test ran to the end' $false $_.Exception.Message
}

Finish $work
