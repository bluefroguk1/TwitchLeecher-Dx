# Handoff: channel monitoring (auto download)

Branch: `claude/feature-auto-download-f77d07`, cut from `combined`.
Written on a Mac with no .NET SDK, so **this code has never been compiled or run**. The first job on Windows is to build and test it.

## First steps on Windows

```powershell
git fetch origin
git checkout claude/feature-auto-download-f77d07
dotnet build TwitchLeecher\TwitchLeecher\TwitchLeecher.csproj -c Release
powershell -ExecutionPolicy Bypass -File test-scripts\test-auto-download.ps1 -SkipBuild
```

The script writes results to `%TEMP%\twitchleecher-auto-download-results-*.txt`. Fix build errors first, then any FAIL rows. It suggests a 5-minute interval, asks for a channel that streams often, and reads the log and seen-list for you.

The script moves any existing `auto_download_seen.json` to `.bak` so the first-run check starts clean.

## What it does

Settings > Auto Download: on/off, channels (comma separated; the star button adds all favourites), "Check every N minutes" (default 15, min 5, max 1440), optional "Also clips with at least N views" (default 100).

While the app runs (after sign-in), a background loop wakes every minute. When the interval has passed it checks each channel:

- VODs: last 20 archive VODs. Clips (if on): clips from the last 7 days (without dates Helix sorts clips by all-time views).
- First check of a channel: existing videos are marked as seen, **not downloaded**. VODs and clips have separate first runs (keys `channel` and `channel#clips`).
- New, not-live VODs and clips at/above the view threshold are queued with the default quality, folder and filename, never overwriting. Live VODs and low-view clips stay unseen and are retried next check.
- Network errors / unknown channels: logged, retried next check, other channels still run.

Files in `%APPDATA%\Twitch Leecher DX\`:
- `auto_download_seen.json`: handled ids per channel. Delete an id to make it download again (that's how the test simulates a new VOD).
- `auto_download.log`: one line per first run, queued video, skip and error. Reset at 1 MB.

## What changed

| Area | File | Notes |
|---|---|---|
| Shared enqueue | `TwitchLeecher.Services/Interfaces/IQuickDownloadService.cs`, `Services/QuickDownloadService.cs` | `CreateDownloadParams`, `TryEnqueue`, `GetSelectedQuality` moved here from `SearchResultViewModel`. Sub-only status now from `IAuthService.IsAuthenticatedSubOnly`. Bound in `ServiceModule`. |
| Search results | `TwitchLeecher.Gui/ViewModels/SearchResultViewModel.cs` | Quick download and Download Selected now call `IQuickDownloadService`. Behaviour should be unchanged; test 8 in the script checks it. |
| Loop | `TwitchLeecher.Gui/Interfaces/IAutoDownloadService.cs`, `Gui/Services/AutoDownloadService.cs` | `Start()` is idempotent; called from `MainWindowVM.ShowWelcomeOrSearch()` (after auth). API calls on the thread pool; `TryEnqueue` and notifications go through `Dispatcher.UIThread`. Bound in `GuiModule`. |
| Settings | `TwitchLeecher.Core/Models/Preferences.cs`, `TwitchLeecher.Services/Services/PreferencesService.cs` | `AutoDownloadEnabled/Interval/Channels/Clips/ClipMinViews`, in `Clone`, validated, saved under `<AutoDownload>` in `config.xml`. |
| UI | `TwitchLeecher.Gui/Views/PreferencesView.axaml`, `ViewModels/PreferencesViewModel.cs` | New "Auto Download" section at outer rows 6/7; Miscellaneous and Experimental moved to 8-11. `UseFavouritesForAutoDownload` `[RelayCommand]` for the star button. |
| Test | `test-scripts/test-auto-download.ps1` | Same style as the other test scripts. |

## Likely build problems (not compiled)

- `await Dispatcher.UIThread.InvokeAsync(() => ...TryEnqueue(...))` inside a ternary in `AutoDownloadService.CheckVideos`.
- Compiled XAML binding to the source-generated `UseFavouritesForAutoDownloadCommand` (the same pattern as `OpenDropdownCommand` already works).
- `NumericUpDown` bound to `int` properties (same as existing `SearchLoadLastVods`).
- Merge conflicts in `PreferencesView.axaml` row numbers with other branches that add Settings sections (tray-notifications, new-theme).

## Known limits

- A skipped video (sub-only, file exists, invalid download settings) is marked seen and not retried; the log says why (`shortcut:` comment).
- Seen ids are never pruned (a few bytes per video; `shortcut:` comment).
- If the download queue is paused, `DownloadService.Enqueue` drops the item silently but it is logged as queued (same as quick download).
- Only checks while the app is running. A VOD that falls out of the last 20 while the app is closed is missed.
- Changing the interval takes effect from the last check time, not instantly.

## Hooking up desktop notifications

The tray branch (`claude/feature-tray-notifications-963477`) adds `IDesktopNotificationService.Notify(title, message)`. Once both are merged, inject it into `AutoDownloadService` and call it at the line marked `// DESKTOP NOTIFICATION:` in `CheckVideos`, e.g. `Notify("Auto download queued", $"{channel} - {video.Title}")`. It's bound in `App.RegisterTypes`, so it resolves fine from the Gui project.

## Rules for this repo

- Never name, link or credit the third-party Twitch downloader project used for ideas.
- End commit messages with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
- Mark known limits as `// shortcut: <limit>, <when to upgrade>`.
