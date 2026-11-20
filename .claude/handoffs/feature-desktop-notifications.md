# feature-desktop-notifications

Branch: `feature-desktop-notifications`, from `claude/feature-tray-notifications-963477` at 4b0930f (after the first-minimise tray prompt).

## What changed
- Desktop notifications are back: the parent of 4d038e7 was reverted in, with the tray branch's later prompt changes kept.
  - `IDesktopNotificationService`;
  - the "Show notifications" setting (`AppShowNotifications`, on by default; `<ShowNotifications>` in config.xml);
  - the "Download finished" and "All N downloads finished" hooks in `DownloadService`;
  - the DesktopNotifications packages and the Windows TFM `net10.0-windows10.0.17763.0` (output still in `bin\<cfg>\net10.0`).
- **Windows identity, the actual fix**, in `TwitchLeecher/DesktopNotificationService.cs`:
  - `WindowsApplicationContext.FromCurrentProcess("Twitch Leecher DX", "TwitchLeecher.Dx")` from the library:
    - (re)writes `%AppData%\Microsoft\Windows\Start Menu\Programs\Twitch Leecher DX.lnk` pointing at the running exe, with `AppUserModelID = TwitchLeecher.Dx`;
    - calls `SetCurrentProcessExplicitAppUserModelID("TwitchLeecher.Dx")`.
  - Toasts are shown through our own `ToastNotificationManager.CreateToastNotifier("TwitchLeecher.Dx")` with a `ToastText02` template. We no longer use the library's `WindowsNotificationManager`.
  - The service is resolved in `App.OnFrameworkInitializationCompleted` before `MainWindow` is created, so the process has its AUMID before any window exists.
  - Toast errors are now logged to the app's `Logs` folder (`*_error.log`) instead of being swallowed.
- Linux is unchanged: the library's FreeDesktop (D-Bus) manager.

## Why toasts never showed before (most likely)
- The old code called `FromCurrentProcess()` with no arguments, so the name and AUMID came from the process main module.
  - The test script ran `dotnet TwitchLeecher.dll`, which gives "dotnet".
- On the windows TFM, the library's `WindowsNotificationManager` creates its notifier through `ToastNotificationManagerCompat.CreateToastNotifier()`. That doesn't use the AUMID it just registered.
- So the shortcut, the process ID and the notifier didn't agree, and Windows dropped the toasts silently. This is from reading the library's master source; the 1.3.1 tag couldn't be fetched to compare.

## Test needs: Windows, one short download (crop to 1 minute)
- Run `test-scripts/test-desktop-notifications.ps1`. On Windows it starts `TwitchLeecher.exe`, not `dotnet ...dll`, so the shortcut targets the real exe. It also checks the `.lnk` and that `Get-StartApps` lists `TwitchLeecher.Dx`.
- **If toasts still don't appear:**
  1. Check Focus assist / Do Not Disturb, and Settings › System › Notifications › "Twitch Leecher DX" (it should be listed and on, once the first toast was attempted).
  2. Look for a new `*_error.log` in `%AppData%\Twitch Leecher DX\Logs`; toast exceptions land there.
  3. Confirm `Get-StartApps | ? AppID -eq TwitchLeecher.Dx` returns a row. If it doesn't, the shortcut has no AUMID; create the `.lnk` ourselves via `IShellLinkW` + `IPropertyStore` (`PKEY_AppUserModel_ID`).
  4. Sign out and in once. Windows sometimes indexes a new Start Menu shortcut late, so the very first toast after the shortcut is created can be dropped.
  5. If all of that checks out but nothing shows, try `ToastNotificationManagerCompat` from `Microsoft.Toolkit.Uwp.Notifications` on its own, without the library. It registers the AUMID in the registry (`HKCU\Software\Classes\AppUserModelId`) instead of via a shortcut.

## Known limits
- When run as `dotnet TwitchLeecher.dll`, the Start Menu shortcut points at `dotnet.exe`. Toasts should still work (same AUMID), but clicking that shortcut won't open the app. It's rewritten to the real exe on the next normal start.
