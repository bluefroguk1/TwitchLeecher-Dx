<!-- title: Off-peak download schedule -->
## Summary

Settings > Download gets a time window (whole hours, may cross midnight).
Outside it, watched-search finds are queued paused as SCHEDULED; when the
window opens they start (optionally with other paused downloads), and when
it closes the downloads the schedule started can pause again. A manual
Resume always starts a download straight away.

## Depends on

the watched searches PR

Until those are merged this PR also shows their changes; please review the last commit only.

## Testing

Not yet tested on Windows.
