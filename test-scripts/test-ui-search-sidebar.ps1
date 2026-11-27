# UI redesign: Search sidebar. Builds, launches, asks y/n/s per check, writes results to %TEMP%.
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$out = Join-Path $env:TEMP 'test-ui-search-sidebar.txt'
Push-Location (Join-Path $root 'TwitchLeecher')
dotnet build -c Release
if ($LASTEXITCODE -ne 0) { "BUILD FAILED" | Set-Content $out; Pop-Location; exit 1 }
$exe = Get-ChildItem -Recurse -Filter TwitchLeecher.exe | Where-Object FullName -like '*Release*' | Select-Object -First 1
Start-Process $exe.FullName
Pop-Location

$checks = @(
 'After sign-in the app opens on Search (no Welcome page)',
 'Sidebar is 300px with Channel | Links / IDs switch, Type chips, Show dropdown and Search button',
 'Right side shows logo, "Twitch Leecher DX <version>" and "The Broadcast Downloader"',
 'Type chips: clicking Highlights selects it and unselects Broadcasts',
 'Show: Custom range shows From/To date pickers; Last N videos shows the number box',
 'Type a channel, press Enter: search runs',
 'Star button opens the favourites dropdown; favourite chips appear under the field (if you have favourites)',
 'Clicking a favourite chip searches that channel',
 '"All favourites" runs a favourites search',
 'Links / IDs: paste one link and one numeric ID on two lines, Search finds both videos',
 'Links / IDs: an invalid line shows an error on the box',
 'Back on Search: Recent searches lists the last searches (max 5), clicking one re-runs it',
 'Restart the app: recent searches are still there',
 'With "Search on Start" on in Settings, restart: the default search runs immediately',
 'Before any search: no "Back to results" link under the Search button',
 'Run a search with results, then click the Search tab: the search form opens (not the results)',
 'The form shows "<- Back to results (N)" with N = number of videos found; clicking it opens the results page',
 'Run a search that finds nothing, click the Search tab: no "Back to results" link',
 'No star button next to the Channel field; favourites show only as chips under it, and clicking a chip searches that channel',
 'No Search / Cancel buttons at the bottom of the Search page; the sidebar Search button and Enter (in the Channel field) still search',
 'Links / IDs: paste or type several links, pressing Enter between them: each goes on its own line, long links wrap inside the box, and a scrollbar appears when there are many',
 'Switch theme New <-> Original in Settings and back to Search: sidebar, chips, switch and empty state follow the theme, all text readable (incl. on the accent)'
)
$results = foreach ($c in $checks) {
  do { $a = Read-Host "$c  [y/n/s]" } while ($a -notin 'y','n','s')
  $note = if ($a -eq 'n') { Read-Host 'What went wrong?' } else { '' }
  "[$a] $c $note"
}
$results | Set-Content $out
Write-Host "Results written to $out"
