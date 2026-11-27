<!-- title: Replace the corner pop-ups with nothing where the result is visible, and a message box where it is not -->
## Summary

The small corner pop-ups ("Preferences saved", "Added to favourites",
"Watching ...", "N download(s) added", "Download added") repeated what the
page already shows, and were easy to miss when they mattered. They are
removed. The one case where a click otherwise gives no feedback, a quick
download that queued nothing (for example "Already in the download queue"),
now shows a message box instead.

## Depends on

every PR that adds a pop-up call (send it last, then rebase).

## Testing

Tested on Windows 11 (2026-11-23) in the combined build.
