<!-- title: Fix second launch not bringing the running window to the front -->
## Summary

App.axaml.cs sent "Activate" on the pipe "TwitchLeecher" while MainWindow
listened on "TwitchLeecher-DX", so starting the app a second time did
nothing. Both now use Constants.PipeName.

## Depends on

—

## Testing

Not yet tested on Windows. To test: start the app, minimise it, start it again; the running window comes to the front.
