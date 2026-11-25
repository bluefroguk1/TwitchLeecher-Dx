# Automated check: downloads stop early, with a clear message, when the drive can't hold them. No clicking needed.
# 1. A normal short download still works (no false alarm).
# 2. A long VOD at source quality, saved to a drive with less free space than it needs, must fail within seconds
#    with "Not enough free space" and write nothing. A watchdog kills it after 60 s and deletes what it wrote, so
#    a broken check can't fill the disk.
# For step 2 the script picks the fixed drive with the least free space; to be sure it can run, point -OutDir at a
# nearly full USB stick. If no drive is small enough, step 2 is skipped (not failed) and the script says so.
# Uses tl-test-lib.ps1. Needs a Release build of this branch and the app signed in once.
#
#   powershell -ExecutionPolicy Bypass -File test-scripts\test-free-space.ps1 [-OutDir E:\tl-test] [-Channel twitch] [-AppDir <folder>]
#
# Exit code 0 = all checks passed, 1 = a check failed, 2 = the test could not run (see the message).
param(
    [string]$OutDir,
    [string]$Channel = 'twitch',
    [string]$AppDir
)

. (Join-Path $PSScriptRoot 'tl-test-lib.ps1')
$work = New-Work 'free-space'

$vods = (Gql @{ query = 'query{user(login:"' + $Channel + '"){videos(first:20,type:ARCHIVE){edges{node{id title lengthSeconds}}}}}' }).data.user.videos.edges.node
if (-not $vods) { Write-Host "CANNOT RUN: no recent broadcasts of '$Channel'."; exit 2 }
$short = $vods | Where-Object { $_.lengthSeconds -ge 900 } | Select-Object -First 1
$long = $vods | Sort-Object lengthSeconds -Descending | Select-Object -First 1

try {
    # 1. no false alarm
    $ok = Invoke-Tl @('download', $short.id, '--quality', 'audio', '--from', '600', '--to', '660', '--out', $work, '--name', 'normal.mp4')
    Check 'a normal download still works' ($ok.Code -eq 0 -and (Test-Path (Join-Path $work 'normal.mp4'))) "exit $($ok.Code)"

    # 2. a download too big for the target drive
    # source quality is roughly 2.5 GB per hour (6 Mbit/s); the app's own estimate is a bit higher
    $roughNeed = $long.lengthSeconds / 3600 * 2.5GB
    if ($OutDir) {
        New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
        $target = (Resolve-Path $OutDir).Path
    }
    else {
        $drive = Get-CimInstance Win32_LogicalDisk -Filter 'DriveType=3' | Sort-Object FreeSpace | Select-Object -First 1
        $target = Join-Path ($drive.DeviceID + '\') 'tl-free-space-test'
    }
    $root = [IO.Path]::GetPathRoot($target)
    $free = (Get-PSDrive -Name $root.Substring(0, 1)).Free
    Write-Host ("Target {0}: {1:N1} GB free; the longest VOD ({2:N1} h) needs roughly {3:N1} GB at source" -f $target, ($free / 1GB), ($long.lengthSeconds / 3600), ($roughNeed / 1GB))

    if ($roughNeed -lt $free * 1.2) {
        Write-Host "SKIPPED step 2: no drive here is small enough to overflow. Run again with -OutDir <a folder on a nearly full USB stick>."
    }
    else {
        New-Item -ItemType Directory -Force -Path $target | Out-Null
        $errFile = Join-Path $work 'stderr.txt'
        $p = Start-Process -FilePath $tl -ArgumentList @('download', $long.id, '--quality', 'source', '--out', "`"$target`"", '--name', 'too-big.mp4') `
            -RedirectStandardError $errFile -RedirectStandardOutput (Join-Path $work 'stdout.txt') -NoNewWindow -PassThru
        $stopped = $p.WaitForExit(60000)
        if (-not $stopped) { Stop-Process -Id $p.Id -Force; Start-Sleep 2 }
        $err = Get-Content -Raw -ErrorAction SilentlyContinue $errFile
        $written = (Get-ChildItem $target -Recurse -File -ErrorAction SilentlyContinue | Measure-Object Length -Sum).Sum

        Check 'stopped by itself within 60 s' $stopped $(if ($stopped) { 'stopped' } else { 'had to be killed: the check did not stop it' })
        Check 'failed (exit 1)' ($stopped -and $p.ExitCode -eq 1) "exit $(if ($stopped) { $p.ExitCode } else { '-' })"
        Check 'says why: not enough free space' ($err -match 'Not enough free space') "$(($err -split "`n" | Where-Object { $_ -match 'free space' } | Select-Object -First 1))"
        Check 'nothing written to the full drive' (-not $written) "$([math]::Round($written / 1MB, 1)) MB"
        Remove-Item $target -Recurse -Force -ErrorAction SilentlyContinue
    }
}
catch {
    Check 'test ran to the end' $false $_.Exception.Message
}

Finish $work
