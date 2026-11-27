<!-- title: Fix second launch not bringing the running window to the front -->
## Summary

App.axaml.cs sent "Activate" on the pipe "TwitchLeecher" while MainWindow
listened on "TwitchLeecher-DX", so starting the app a second time did
nothing. Both now use Constants.PipeName.

## Depends on

—

## Testing

Tested on Windows 11 (2026-11-23), in the combined build: with the app running, starting it a second time brings the existing window to the front instead of opening another copy.
