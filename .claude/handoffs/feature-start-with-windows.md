# feature-start-with-windows

Branch: `feature-start-with-windows`, from `claude/feature-tray-notifications-963477` at 4b0930f (after the first-minimise tray prompt).

**Depends on `fix-single-instance-pipe`.** Its fix commit ad246d1 is cherry-picked here as 2eab27a. A hidden start relies on a second launch reaching the running instance over the pipe. When merging both into `combined-all`, git sees the same change twice; that's fine.

## What changed
- New preferences `AppStartWithOs` and `AppStartMinimised` (both default false): `Preferences.cs` (field, property, `Clone`), and `PreferencesService` (save, load, defaults). Stored as `<StartWithOs>`/`<StartMinimised>` under `<Application>`.
- Settings › Application has two new checkboxes:
  - "Start with the system";
  - "Start minimised to the tray", enabled only while the first one is ticked.
- New `TwitchLeecher/AutoStart.cs` (app project):
  - **Windows:** `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, value `TwitchLeecher` = `"<exe>"`, plus ` --minimized` when start minimised is on. Per user, no admin. Guarded by `OperatingSystem.IsWindows()`.
  - **Linux:** `$XDG_CONFIG_HOME/autostart/twitchleecher.desktop` (usually `~/.config/autostart`).
  - Turning the option off removes the entry.
  - When the app runs as `dotnet TwitchLeecher.dll`, the entry is `"dotnet" "<dir>/TwitchLeecher.dll"`.
- `App.axaml.cs`:
  - `AutoStart.Apply` runs at start-up, which re-syncs the path if the exe moved. It also runs on every `PreferencesSavedEvent` (strong subscription). Failures are logged, never shown.
  - With `--minimized`, `MainWindow` is still created, so the pipe server runs, but it's assigned to `desktop.MainWindow` only after the lifetime's start-up `Show()`. So it stays hidden; the tray icon, the tray Open entry or a second launch shows it.
  - Tray › Exit: when no window was ever opened (`Windows.Count == 0`), it calls `desktop.Shutdown()`, because there's no window to close.

## Known limits
- The Linux `Exec=` path isn't escaped for `" \` $ \`. This is marked `shortcut:` in the code.
- The hidden start depends on Avalonia showing `MainWindow` in `Start()` before the posted assignment runs. If the window ever flashes or shows on a `--minimized` start, that's the cause. The fix then is to show and immediately hide it in `Opened`.
- `viewModel.Shown()` (startup search, update check) runs the first time the window is shown, not at a hidden start.

## Test
- `test-scripts/test-start-with-windows.ps1`, then sign out and in and run it again with `-SkipBuild -AfterSignIn`.
- Test needs: Windows (and ideally a Linux desktop). The final check needs a sign-out/sign-in of Windows or a reboot.
