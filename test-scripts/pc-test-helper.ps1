# Setup and checking steps for the automated PC test run (see .claude/handoffs/LATEST.md, "AUTOMATED PC TEST RUN").
# The GUI itself is driven by Claude with computer use; this script does everything that doesn't need the mouse.
#
#   powershell -ExecutionPolicy Bypass -File test-scripts\pc-test-helper.ps1 <Action> [options]
#
# Actions
#   Backup                       copy the settings folder to %TEMP%\tl-test-backup (do this FIRST)
#   Restore                      put the backed-up settings back (do this LAST, with the app closed)
#   SetLastVersion -Version 3.9.1   make the app think an older version ran last (re-shows the What's new card)
#   SetTheme -Theme New|Original|Coyote   New = Dark (purple), Original = Classic (gold), Coyote
#   FreshInstall                 move the settings folder aside so the app starts as a fresh install (Restore undoes it)
#   Start [-Minimized]           start the built app (closes any running copy first)
#   Stop                         close every running copy
#   Cli -CliArgs '...'           run tl with these arguments; prints its output and the exit code
#   Errors                       list error logs written since Backup, with their first lines
#   Show                         print the Version in runtime.xml and the Theme / StartMinimised / CheckForUpdates in config.xml
param(
    [Parameter(Mandatory, Position = 0)]
    [ValidateSet('Backup', 'Restore', 'SetLastVersion', 'SetTheme', 'FreshInstall', 'Start', 'Stop', 'Cli', 'Errors', 'Show')]
    [string]$Action,
    [string]$Version = '3.9.1',
    [ValidateSet('New', 'Original', 'Coyote')][string]$Theme = 'New',
    [switch]$Minimized,
    [string]$CliArgs = '--help',
    # folder holding TwitchLeecher.exe and tl.exe; default: the Release build output in this checkout
    [string]$AppDir
)

$ErrorActionPreference = 'Stop'
$settings = Join-Path $env:APPDATA 'Twitch Leecher DX'
$backup = Join-Path $env:TEMP 'tl-test-backup'
$stamp = Join-Path $backup '.taken'
$root = Split-Path -Parent $PSScriptRoot

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
    throw "$name not found. Build first (dotnet build TwitchLeecher\TwitchLeecher.sln -c Release) or pass -AppDir."
}

function Stop-App {
    Get-Process TwitchLeecher -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Milliseconds 500
}

function Edit-Xml([string]$file, [scriptblock]$change) {
    if (-not (Test-Path $file)) { throw "$file not found. Start the app once first." }
    [xml]$x = Get-Content -Raw -Encoding UTF8 $file
    & $change $x
    $x.Save($file)
}

switch ($Action) {
    'Backup' {
        Stop-App
        if (Test-Path $stamp) { throw "A backup already exists in $backup. Run Restore first, or delete that folder if it is stale." }
        New-Item -ItemType Directory -Force -Path $backup | Out-Null
        if (Test-Path $settings) { Copy-Item -Path (Join-Path $settings '*') -Destination $backup -Recurse -Force }
        Set-Content -Path $stamp -Value (Get-Date -Format o)
        Write-Host "Settings backed up to $backup"
    }
    'Restore' {
        Stop-App
        if (-not (Test-Path $stamp)) { throw "No backup in $backup." }
        Get-ChildItem -Path (Split-Path $settings) -Filter 'Twitch Leecher DX.fresh-*' -Directory -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force
        if (Test-Path $settings) { Remove-Item $settings -Recurse -Force }
        New-Item -ItemType Directory -Force -Path $settings | Out-Null
        Get-ChildItem -Path $backup -Force | Where-Object { $_.Name -ne '.taken' } | Copy-Item -Destination $settings -Recurse -Force
        Remove-Item $backup -Recurse -Force
        Write-Host "Settings restored to $settings"
    }
    'SetLastVersion' {
        Stop-App
        Edit-Xml (Join-Path $settings 'runtime.xml') { param($x) $x.DocumentElement.SetAttribute('Version', $Version) }
        Write-Host "runtime.xml Version set to $Version"
    }
    'SetTheme' {
        Stop-App
        Edit-Xml (Join-Path $settings 'config.xml') {
            param($x)
            $el = $x.SelectSingleNode('//Application/Theme')
            if (-not $el) { throw 'No Application/Theme in config.xml. Save Settings once in the app first.' }
            $el.InnerText = $Theme
        }
        Write-Host "Theme set to $Theme"
    }
    'FreshInstall' {
        Stop-App
        if (-not (Test-Path $stamp)) { throw 'Run Backup first.' }
        if (Test-Path $settings) { Rename-Item $settings ("Twitch Leecher DX.fresh-" + (Get-Date -Format yyyyMMddHHmmss)) }
        Write-Host 'Settings folder moved aside: the next Start is a fresh install. Restore puts everything back.'
    }
    'Start' {
        Stop-App
        $exe = Find-Exe 'TwitchLeecher.exe'
        if ($Minimized) { Start-Process $exe -ArgumentList '--minimized' } else { Start-Process $exe }
        Write-Host "Started $exe $(if ($Minimized) { '--minimized' })"
    }
    'Stop' { Stop-App; Write-Host 'Stopped' }
    'Cli' {
        $tl = Find-Exe 'tl.exe'
        Write-Host "> tl $CliArgs"
        $p = Start-Process -FilePath $tl -ArgumentList $CliArgs -NoNewWindow -Wait -PassThru
        Write-Host "exit code: $($p.ExitCode)"
    }
    'Errors' {
        $since = if (Test-Path $stamp) { [datetime](Get-Content $stamp) } else { (Get-Date).AddHours(-6) }
        $logs = Get-ChildItem -Path (Join-Path $settings 'logs') -Filter '*_error.log' -ErrorAction SilentlyContinue | Where-Object { $_.LastWriteTime -ge $since }
        if (-not $logs) { Write-Host "No error logs since $since" }
        foreach ($l in $logs) { Write-Host "== $($l.FullName)"; Get-Content $l.FullName -TotalCount 15 }
    }
    'Show' {
        $rt = Join-Path $settings 'runtime.xml'
        $cf = Join-Path $settings 'config.xml'
        if (Test-Path $rt) { [xml]$r = Get-Content -Raw $rt; Write-Host "runtime.xml Version: $($r.DocumentElement.GetAttribute('Version'))" } else { Write-Host 'runtime.xml: none' }
        if (Test-Path $cf) {
            [xml]$c = Get-Content -Raw $cf
            foreach ($n in 'Theme', 'StartMinimised', 'CheckForUpdates', 'MinimiseToTray') { Write-Host "config.xml ${n}: $($c.SelectSingleNode("//Application/$n").InnerText)" }
        } else { Write-Host 'config.xml: none' }
    }
}
