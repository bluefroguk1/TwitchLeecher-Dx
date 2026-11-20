# fix-single-instance-pipe

Branch: `fix-single-instance-pipe`, from `master`.

## What changed
- A second launch sent "Activate" over the pipe `TwitchLeecher`, but `MainWindow` listens on `TwitchLeecher-DX`. So the second launch exited without bringing the running window forward.
- New `Constants.PipeName` (`TwitchLeecher-DX`) in `TwitchLeecher.Core/Constants/Constants.cs`. `App.axaml.cs` (sender) and `MainWindow.axaml.cs` (listener) both use it.

## Test
- `test-scripts/test-fix-single-instance-pipe.ps1`
- Test needs: Windows or Linux desktop; start the app twice.

## Notes
- `feature-start-with-windows` cherry-picks this commit, because a hidden start relies on a second launch waking it.
