<!-- title: Option to start with Windows, minimised to the tray -->
## Summary

Settings get "Start Twitch Leecher when I sign in" and "Start minimised to
the tray". On Windows this writes a per-user Run entry (no admin needed),
on Linux an autostart .desktop file; both are removed when switched off.

## Depends on

the tray PR and the single-instance fix

Until those are merged this PR also shows their changes; please review the last commit only.

## Testing

Not yet tested on Windows.
