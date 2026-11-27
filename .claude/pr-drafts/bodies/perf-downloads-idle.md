<!-- title: Downloads page: only refresh every second while something downloads -->
## Summary

The one-second refresh of the Downloads page ran for ever, even with an empty queue. It now runs only while a download is downloading or queued; otherwise the page updates once whenever a download is added, removed or changes state.

## Depends on

the Downloads page PR

Until that is merged this PR also shows its changes; please review the last commit only.

## Testing

Not yet tested on Windows.
