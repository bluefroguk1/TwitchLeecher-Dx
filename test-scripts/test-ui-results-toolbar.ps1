# Search results redesign checks. Test needs: instant (UI), then a quick download.
$ErrorActionPreference = 'Stop'
$out = Join-Path $env:TEMP 'test-ui-results-toolbar.txt'
$root = Split-Path -Parent $PSScriptRoot
$proj = Join-Path $root 'TwitchLeecher\TwitchLeecher\TwitchLeecher.csproj'
dotnet build $proj -c Release
if ($LASTEXITCODE -ne 0) { "BUILD FAILED" | Out-File $out; exit 1 }
$exe = Join-Path (Split-Path -Parent $proj) 'bin\Release\net10.0\TwitchLeecher.exe'
Start-Process $exe
$questions = @(
  'Search a channel (Broadcasts, last 10). Summary bar shows "channel · Broadcasts · last 10 · N results" and Edit search goes back?',
  'Cards are 3 per row, 2-line title, date · views meta line, labelled Download button?',
  'Ticking a card checkbox shows a purple ring, footer shows "1 selected" and "Download 1 video"?',
  'After a moment the footer shows an approximate size (~X GB)?',
  'Sort dropdown: Oldest / Longest / Most viewed reorder the cards correctly?',
  'List toggle shows one compact row per video; grid toggle returns?',
  'Select all ticks every non-live video; unticking clears them?',
  'Clip search: cards show CLIP badge and "Clip by X" in the meta line?',
  'Click Download on one video: card shows QUEUED badge?',
  'After that download finishes, return to results: card shows DOWNLOADED, and Hide downloaded hides it?',
  'Download N videos queues the ticked videos?',
  'Switch theme New <-> Original in Settings and return to results: colours follow the theme, text on the accent button and CLIP badge is readable?'
)
$results = foreach ($q in $questions) { $a = Read-Host "$q (y/n/s)"; "$a`t$q" }
$results | Out-File $out
Write-Host "Results written to $out"
