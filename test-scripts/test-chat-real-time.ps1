# Automated check: the "Chat as text: show the time each message was sent" setting. No clicking needed.
# Downloads the same short audio-only crop twice with `tl download --chat text`: once with the setting off
# ([h:mm:ss] into the video) and once on ([yyyy-MM-dd HH:mm:ss] of day). The setting is switched in
# config.xml, which is copied first and always put back.
# Uses tl-test-lib.ps1. Needs a Release build of this branch and the app signed in once (and closed).
#
#   powershell -ExecutionPolicy Bypass -File test-scripts\test-chat-real-time.ps1 [-Channel twitch] [-AppDir <folder>]
#
# Exit code 0 = all checks passed, 1 = a check failed, 2 = the test could not run (see the message).
param(
    [string]$Channel = 'twitch',
    [string]$AppDir
)

. (Join-Path $PSScriptRoot 'tl-test-lib.ps1')
$work = New-Work 'chat-real-time'
$config = Join-Path $env:APPDATA 'Twitch Leecher DX\config.xml'
if (-not (Test-Path $config)) { Write-Host "CANNOT RUN: $config not found. Start the app and save Settings once."; exit 2 }
if (Get-Process TwitchLeecher -ErrorAction SilentlyContinue) { Write-Host 'CANNOT RUN: close Twitch Leecher first (it would overwrite the setting).'; exit 2 }
Copy-Item $config (Join-Path $work 'config.backup.xml') -Force

function Set-RealTime([bool]$on) {
    [xml]$x = Get-Content -Raw -Encoding UTF8 $config
    $download = $x.SelectSingleNode('//Download')
    $el = $download.SelectSingleNode('ChatRealTime')
    if (-not $el) { $el = $x.CreateElement('ChatRealTime'); [void]$download.AppendChild($el) }
    $el.InnerText = $(if ($on) { 'True' } else { 'False' })
    $x.Save($config)
}

$vod = Find-Vod $Channel 1200
if (-not $vod) { Write-Host "CANNOT RUN: no recent broadcast of '$Channel' of 20+ minutes. Try -Channel <another channel>."; exit 2 }
Write-Host "VOD $($vod.id): $($vod.title)"

try {
    $info = (Invoke-Tl @('info', $vod.id)).Out | ConvertFrom-Json
    $start = [datetime]::Parse($info.recordedDate, [Globalization.CultureInfo]::InvariantCulture, [Globalization.DateTimeStyles]::RoundtripKind).ToLocalTime()
    $from = 600; $to = 900

    $lines = @{}
    foreach ($on in @($false, $true)) {
        Set-RealTime $on
        $name = "realtime-$on.mp4"
        $d = Invoke-Tl @('download', $vod.id, '--quality', 'audio', '--from', "$from", '--to', "$to", '--out', $work, '--name', $name, '--chat', 'text')
        Check "download with the setting $(if ($on) { 'on' } else { 'off' }) exits 0" ($d.Code -eq 0) "exit $($d.Code)"
        $file = Join-Path $work "realtime-$on.chat.txt"
        $lines[$on] = @(Get-Content -Encoding UTF8 $file -ErrorAction SilentlyContinue | Where-Object { $_ })
    }

    if ($lines[$false].Count -eq 0) { Write-Host 'CANNOT RUN: the sampled 5 minutes had no chat. Try -Channel <a busier channel>.'; exit 2 }

    $offBad = @($lines[$false] | Where-Object { $_ -notmatch '^\[\d{1,2}:\d{2}:\d{2}\] ' })
    Check 'off: [h:mm:ss] into the video' ($offBad.Count -eq 0) "$($lines[$false].Count) lines, $($offBad.Count) other"
    $onBad = @($lines[$true] | Where-Object { $_ -notmatch '^\[\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\] ' })
    Check 'on: [date time of day]' ($onBad.Count -eq 0) "$($lines[$true].Count) lines, $($onBad.Count) other"
    Check 'same messages both ways' ($lines[$true].Count -eq $lines[$false].Count) "$($lines[$true].Count) vs $($lines[$false].Count)"

    # the first message's time of day = the stream start + its offset (crop start + its time into the crop)
    $first = [datetime]::ParseExact(($lines[$true][0] -replace '^\[([^\]]+)\].*', '$1'), 'yyyy-MM-dd HH:mm:ss', $null)
    $into = [timespan]::Parse(($lines[$false][0] -replace '^\[([^\]]+)\].*', '$1'))
    $expected = $start.AddSeconds($from) + $into
    Check 'time of day matches the stream start + offset' ([math]::Abs(($first - $expected).TotalSeconds) -le 2) "$first, expected about $expected"
}
catch {
    Check 'test ran to the end' $false $_.Exception.Message
}
finally {
    Copy-Item (Join-Path $work 'config.backup.xml') $config -Force
    Write-Host 'Settings file put back as it was.'
}

Finish $work
