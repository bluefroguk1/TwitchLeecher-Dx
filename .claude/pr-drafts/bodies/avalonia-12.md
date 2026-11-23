<!-- title: Update to Avalonia 12 -->
## Summary

Moves every Avalonia package to 12.1.6: DataGrid 12.1.2, and the progress ring 0.11.0 (its first Avalonia 12 release).
- `Avalonia.Diagnostics` isn't published for Avalonia 12 (the dev tools moved to a separate product). The app never attached them, so the Debug-only reference is removed.
- `IClipboard.SetTextAsync` is now an extension method, so `LogViewModel` adds `using Avalonia.Input.Platform`.
- Compiled bindings are now the default in every project, and all of the views' bindings compile.

## Depends on

- The ReactiveUI removal (`Avalonia.ReactiveUI` isn't published for 12).
- The icon change (the old icon package crashes on 12).

This draft includes both as its first two commits, so please review only the last one. It touches every project file, so it's best merged after the other UI pull requests.

## Testing

Tested on Windows 11 (2026-11-23): styled controls (date picker, calendar, toggle switch, combo box, scroll bars), header, tray, the log grid and Log > Copy. Includes a start-up fix: Avalonia 12 only resolves `avares://` assets from loaded assemblies, so `App` loads the Gui assembly before reading the tray icon.
