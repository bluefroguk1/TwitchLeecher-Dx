# Shared styles refactor checks (skeleton placeholder + accent ToggleSwitch). Test needs: instant (UI only).
$ErrorActionPreference = 'Stop'
$out = Join-Path $env:TEMP 'test-refactor-shared-styles.txt'
$root = Split-Path -Parent $PSScriptRoot
$proj = Join-Path $root 'TwitchLeecher\TwitchLeecher\TwitchLeecher.csproj'
dotnet build $proj -c Release
if ($LASTEXITCODE -ne 0) { "BUILD FAILED" | Out-File $out; exit 1 }
$exe = Join-Path (Split-Path -Parent $proj) 'bin\Release\net10.0\TwitchLeecher.exe'
Start-Process $exe
$questions = @(
  'Search tab, New theme: while a search loads, the grey placeholder blocks pulse softly?',
  'Search tab, New theme: sidebar/recent loading placeholders also pulse (if you see any)?',
  'Open a video Download dialog, New theme: preview picture placeholders pulse while loading?',
  'Results toolbar, New theme: Hide downloaded switch is grey when off, accent colour when on (also while hovering)?',
  'Settings > Watched searches, New theme: On/Off switch is grey when off and accent when on, with the "On"/"Off" label still shown?',
  'Switch to Original theme (Settings) and repeat the placeholder pulse, Hide downloaded and watched-searches switch checks: all look right?',
  'Switch to Coyote theme and repeat the same checks: all look right?'
)
$results = foreach ($q in $questions) { $a = Read-Host "$q (y/n/s)"; "$a`t$q" }
$results | Out-File $out
Write-Host "Results written to $out"
