# Handoff: tray icon + desktop notifications

Branch: `claude/feature-tray-notifications-963477`, cut from `combined`.
Written on a Mac with no .NET SDK, so **this code has never been compiled or run**. The first job on Windows is to build and test it.

> **Update 2026-11-19: desktop notifications were removed from this branch.** On the Windows PC the toast backend initialised and
> `ShowNotification` returned without error, but no toast ever appeared (Do Not Disturb was checked). The tray icon and "Minimise to tray"
> were tested and work. Removed: `IDesktopNotificationService`/`DesktopNotificationService`, the "Show notifications" setting, the
> download hooks, the `DesktopNotifications*` packages and the Windows-specific target framework (back to `net10.0`).
> To revisit: toast identity (AppUserModelID + Start Menu shortcut) for an unpackaged exe, or a packaged/MSIX build.

## First steps on Windows

```powershell
git fetch origin
git checkout claude/feature-tray-notifications-963477
dotnet build TwitchLeecher\TwitchLeecher\TwitchLeecher.csproj -c Release
powershell -ExecutionPolicy Bypass -File test-scripts\test-tray-notifications.ps1 -SkipBuild
```

The script writes results to `%TEMP%\twitchleecher-tray-notifications-results-*.txt`. Fix build errors first, then any FAIL rows.

## What changed

| Area | File | Notes |
|---|---|---|
| Settings | `TwitchLeecher.Core/Models/Preferences.cs`, `TwitchLeecher.Services/Services/PreferencesService.cs`, `TwitchLeecher.Gui/Views/PreferencesView.axaml` | `AppMinimiseToTray` (default off), `AppShowNotifications` (default on). Same pattern as `AppShowDonationButton`. |
| Tray | `TwitchLeecher/App.axaml`, `App.axaml.cs` | Avalonia `TrayIcon`, icon `avares://TwitchLeecher.Gui/Assets/TL_Icon.ico`, menu Open/Exit. Exit calls `MainWindow.Close()` so window state is saved. |
| Minimise to tray | `TwitchLeecher.Gui/Views/MainWindow.axaml.cs` | `PropertyChanged` on `WindowState` → `Hide()`. Activation logic moved into public `BringToFront()` (also calls `Show()`), used by tray and the pipe "Activate" message. |
| Notifications | `TwitchLeecher.Services/Interfaces/IDesktopNotificationService.cs`, `TwitchLeecher/DesktopNotificationService.cs` | `Notify(title, message)`: fire-and-forget, never throws, no-op if setting off or backend fails. Bound in `App.RegisterTypes` (not `ServiceModule`) because only the app project has the Windows TFM. |
| Download hooks | `TwitchLeecher.Services/Services/DownloadService.cs` | In the download continuation: "Download finished" + video title on Done/CompletedWithWarning; "All N downloads finished" when `CanShutdown()` and N > 1. |
| Build | `TwitchLeecher/TwitchLeecher.csproj` | Packages `DesktopNotifications`, `.FreeDesktop`, `.Windows` (Windows only) 1.3.1. On Windows the TFM is `net10.0-windows10.0.17763.0`; output pinned to `bin\Release\net10.0` so other test scripts still work. |
| Test | `test-scripts/test-tray-notifications.ps1` | Same style as the other test scripts. |

## Why the Windows TFM

`DesktopNotifications.Windows` for plain `net10.0` resolves its `netcoreapp3.1` build, which uses the old built-in WinRT interop that .NET 5+ removed. Only its `net6.0-windows10.0.17763` build works, so the app project needs a Windows TFM. Code uses `#if WINDOWS` to pick `WindowsNotificationManager`, else `FreeDesktopNotificationManager`.

## Likely build problems (API written from memory, not compiled)

- `WindowsApplicationContext.FromCurrentProcess()` / `FreeDesktopApplicationContext.FromCurrentProcess()` signatures, and `new Notification { Title, Body }` / `ShowNotification(notification)`.
- XAML event handlers on `TrayIcon.Clicked` / `NativeMenuItem.Click` in `App.axaml` (handlers are `object sender, EventArgs e`).
- `Window.Close()` on a hidden window should fire `Closed` and exit the app; check.

## Known limits

- Avalonia has no tray double-click event; a single click restores (double-click works too).
- "All N" counts every ended download, including failed or cancelled. Removing the last queued items by hand skips the counter reset (`shortcut:` comment in `DownloadService`).
- Windows toast registration may add a Start Menu shortcut on first notification.
- Linux with no tray host: a window minimised to tray can't be restored. Setting is off by default.
- macOS: no notification backend.

## Existing bug spotted, not fixed

`App.axaml.cs` sends "Activate" to pipe `TwitchLeecher`, but `MainWindow` listens on `TwitchLeecher-DX`, so a second launch probably doesn't bring the running window forward (including from the tray). Worth a separate fix.

## Related work

The auto-download session may call `IDesktopNotificationService.Notify("New VOD downloaded", title)`.

## Rules for this repo

- Never name, link or credit the third-party Twitch downloader project used for ideas.
- End commit messages with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
- Mark known limits as `// shortcut: <limit>, <when to upgrade>`.
