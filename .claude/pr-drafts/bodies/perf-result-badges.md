<!-- title: Search results: cheaper badge refresh, and DOWNLOADED when a download finishes -->
## Summary

Result badges were rebuilt for every result on each download-queue change (a filename build, File.Exists and a queue search per result, on the UI thread), and a finished download never turned its result into DOWNLOADED. Default paths are now cached, each folder is listed once, queue events are coalesced, and download state changes trigger a refresh.

## Depends on

the search results toolbar PR

Until that is merged this PR also shows its changes; please review the last commit only.

## Testing

Not yet tested on Windows.
