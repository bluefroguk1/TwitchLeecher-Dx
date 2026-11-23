<!-- title: Downloads page: speed, time left and per-download pause -->
## Summary

- Each download shows its speed and time left; a summary bar shows overall
  progress with Pause all and Clear finished.
- Downloads can be paused and resumed individually; finished chunks are
  kept. New downloads added while the queue is paused are queued instead
  of being dropped.
- Buttons fit each state (Pause/Cancel, Move up/Remove, Resume, Play/Open
  folder, Retry/View log) and failed downloads show a short reason.
- Cancel stops in-flight chunk requests at once, and the card says
  "Cancelling" while temporary files are cleaned up.

## Depends on

the theme colours PR (and a rebase after #104, #111, #113 and #115)

Until those are merged this PR also shows their changes; please review the last commit only.

## Testing

Tested on Windows 11 (2026-11-23): per-download pause/resume, speed and time left, failure reasons, and Cancel (shows "Cancelling" while it cleans up).
