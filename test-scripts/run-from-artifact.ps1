# Runs any test script against a build from GitHub Actions, so the test PC needs no build.
# 1. On GitHub open Actions > Build, pick the run for your branch, and download
#    TwitchLeecher-<branch>-win-x64 (or -linux-x64) from the Artifacts list at the bottom.
# 2. Unzip it to a folder.
# 3. Run, from a checkout that has the test scripts:
#    Windows:  powershell -ExecutionPolicy Bypass -File test-scripts\run-from-artifact.ps1 -AppDir C:\Downloads\TwitchLeecher-combined-win-x64 -Test test-scripts\test-bugfixes.ps1
#    Linux:    pwsh test-scripts/run-from-artifact.ps1 -AppDir ~/Downloads/TwitchLeecher-combined-linux-x64 -Test test-scripts/test-bugfixes.ps1
# It copies the app into the folder the test scripts expect (TwitchLeecher/TwitchLeecher/bin/Release/net10.0),
# replacing any local build there, then runs the test script with -SkipBuild.
# shortcut: the test scripts start the app as "dotnet TwitchLeecher.dll", so the dotnet command must still be installed; give the test scripts their own -AppDir if a PC without .NET has to test
param(
    [Parameter(Mandatory)][string]$AppDir,
    [Parameter(Mandatory)][string]$Test
)

$ErrorActionPreference = 'Stop'
if (-not (Test-Path (Join-Path $AppDir 'TwitchLeecher.dll'))) { throw "TwitchLeecher.dll not found in $AppDir. Point -AppDir at the unzipped artifact folder." }
$Test = (Resolve-Path $Test).Path
$root = Split-Path -Parent (Split-Path -Parent $Test)
$out = Join-Path $root (Join-Path 'TwitchLeecher' (Join-Path 'TwitchLeecher' (Join-Path 'bin' (Join-Path 'Release' 'net10.0'))))

if (Test-Path $out) { Remove-Item $out -Recurse -Force }
New-Item -ItemType Directory -Path $out | Out-Null
Copy-Item -Path (Join-Path $AppDir '*') -Destination $out -Recurse
Write-Host "Copied $AppDir to $out"

& $Test -SkipBuild
