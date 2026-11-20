<!-- title: Desktop notifications when downloads finish -->
## Summary

Shows a notification when a download finishes and "All N downloads
finished" after a batch (setting, on by default). On Windows the app
registers a Start Menu shortcut with an AppUserModelID, which Windows needs
before it shows notifications from an unpackaged app.

## Depends on

the tray PR

Until those are merged this PR also shows their changes; please review the last commit only.

## Testing

Not yet tested on Windows (an earlier version without the AppUserModelID showed no notifications).
