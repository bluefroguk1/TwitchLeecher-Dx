<!-- title: Watched searches: automatically download new VODs and clips -->
## Summary

- Save a channel search ("Watch this search" on the results page, or
  Settings > Watched searches) and the app checks it on an interval while
  it runs.
- Each search has its own type, minimum clip views, quality and "When
  found": Download now, or Add to queue paused.
- The first check only records what already exists. A stream that is still
  live is held back (Helix /streams) until it ends.
- Settings shows each search's latest result, an activity list and Check
  now; the Downloads page shows how many searches are active.

## Depends on

the Settings PR, the Downloads page PR, #106 and #108

Until those are merged this PR also shows their changes; please review the last commit only.

## Testing

Tested on Windows: watched searches, first run, Download now / paused. Holding back live streams and the new row layout are not yet re-tested.
