# Automated check: the "Max download speed" setting. No clicking needed.
# Downloads the same 3-minute video crop with `tl` twice: with a 2 MB/s limit, then with none, timing each.
# The limited one must average at most 2 MB/s (15% leeway). The setting is switched in config.xml, which is
# copied first and always put back.
# Uses tl-test-lib.ps1. Needs a Release build of this branch, the app signed in once (and closed), and a
# connection faster than 2 MB/s for the comparison. About 2-3 minutes and 100-200 MB.
#
#   powershell -ExecutionPolicy Bypass -File test-scripts\test-speed-limit.ps1 [-Channel twitch] [-LimitMB 2] [-AppDir <folder>]
#
# Exit code 0 = all checks passed, 1 = a check failed, 2 = the test could not run (see the message).
param(
    [string]$Channel = 'twitch',
    [int]$LimitMB = 2,
    [string]$AppDir
)

. (Join-Path $PSScriptRoot 'tl-test-lib.ps1')
$work = New-Work 'speed-limit'
$config = Join-Path $env:APPDATA 'Twitch Leecher DX\config.xml'
if (-not (Test-Path $config)) { Write-Host "CANNOT RUN: $config not found. Start the app and save Settings once."; exit 2 }
if (Get-Process TwitchLeecher -ErrorAction SilentlyContinue) { Write-Host 'CANNOT RUN: close Twitch Leecher first (it would overwrite the setting).'; exit 2 }
Copy-Item $config (Join-Path $work 'config.backup.xml') -Force

function Set-MaxSpeed([int]$mb) {
    [xml]$x = Get-Content -Raw -Encoding UTF8 $config
    $download = $x.SelectSingleNode('//Download')
    $el = $download.SelectSingleNode('MaxSpeed')
    if (-not $el) { $el = $x.CreateElement('MaxSpeed'); [void]$download.AppendChild($el) }
    $el.InnerText = "$mb"
    $x.Save($config)
}

$vod = Find-Vod $Channel 1200
if (-not $vod) { Write-Host "CANNOT RUN: no recent broadcast of '$Channel' of 20+ minutes. Try -Channel <another channel>."; exit 2 }
Write-Host "VOD $($vod.id): $($vod.title)"

# a mid quality: 720p if there is one, else the middle of the list
$qualities = @((Invoke-Tl @('qualities', $vod.id)).Out -split "`n" | Where-Object { $_ -and $_ -notmatch 'audio' })
$quality = ($qualities | Where-Object { $_ -match '^720p' } | Select-Object -First 1)
if (-not $quality) { $quality = $qualities[[int]($qualities.Count / 2)] }
$quality = ($quality -split ' ')[0]
Write-Host "Quality: $quality"

# Downloads 3 minutes and returns MB/s (file size over wall time; conversion is a quick copy)
function Timed([string]$name) {
    $sw = [Diagnostics.Stopwatch]::StartNew()
    $d = Invoke-Tl @('download', $vod.id, '--quality', $quality, '--from', '600', '--to', '780', '--out', $work, '--name', $name)
    $sw.Stop()
    if ($d.Code -ne 0) { throw "tl download exited with $($d.Code)" }
    $mb = (Get-Item (Join-Path $work $name)).Length / 1MB
    return @{ MB = [math]::Round($mb, 1); Seconds = [math]::Round($sw.Elapsed.TotalSeconds, 1); Rate = [math]::Round($mb / $sw.Elapsed.TotalSeconds, 2) }
}

try {
    Set-MaxSpeed $LimitMB
    $limited = Timed 'limited.mp4'
    Check "limited to $LimitMB MB/s on average" ($limited.Rate -le $LimitMB * 1.15) "$($limited.MB) MB in $($limited.Seconds) s = $($limited.Rate) MB/s"
    Check 'enough data to measure' ($limited.MB -ge $LimitMB * 10) "$($limited.MB) MB (needs $($LimitMB * 10)+ MB for a fair average)"

    Set-MaxSpeed 0
    $free = Timed 'unlimited.mp4'
    Write-Host "No limit: $($free.MB) MB in $($free.Seconds) s = $($free.Rate) MB/s"
    if ($free.Rate -gt $LimitMB * 1.5) {
        Check 'no limit (0) is faster' ($free.Rate -gt $limited.Rate * 1.3) "$($free.Rate) MB/s vs $($limited.Rate) MB/s limited"
    }
    else {
        Write-Host "NOTE: this connection only managed $($free.Rate) MB/s without a limit, so the comparison was skipped. Try -LimitMB 1."
    }
}
catch {
    Check 'test ran to the end' $false $_.Exception.Message
}
finally {
    Copy-Item (Join-Path $work 'config.backup.xml') $config -Force
    Write-Host 'Settings file put back as it was.'
}

Finish $work
