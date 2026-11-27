# ui-header-nav: new header + corner toasts

**Branch:** `claude/ui-header-nav-d65209` (on top of `ui-theme-keys`)
**Depends on:** ui-theme-keys (merge that first)
**Test needs:** instant (UI only)
**Test script:** `test-scripts/test-ui-header-nav.ps1`

## What changed
- `TwitchLeecher.Gui/Views/MainWindow.axaml`
  - Header: 56px `#18181B` bar, logo + "Twitch Leecher", Search / Downloads tabs (count badge, 2px `#9146FF` underline on the active page), right side "Sub-only: on/off", "Settings", and three icon buttons: Info, Donate (only if `ShowDonationButton`) and Log out (`RevokeCommand`). They were an Account dropdown at first; the user asked for plain buttons.
  - Every header button has `ToolTip.Tip` and `AutomationProperties.Name`.
  - The purple top notification bar is replaced by a bottom-right toast (card `#2A2A30`, rounded, 0.3 s opacity fade). It ignores clicks while hidden.
  - All colours are `{DynamicResource <Key>Brush}` theme keys from `ui-theme-keys`, so the header and toast follow the New/Original theme. Only the toast shadow (`#66000000`) is fixed.
  - Header styles live in `Window.Styles` in this file; `App.axaml` is untouched.
- `TwitchLeecher.Gui/ViewModels/MainWindowVM.cs`
  - Added `IsSearchActive` (Search or SearchResult view), `IsDownloadsActive` and `SubOnlyLabel`, all raised from the existing setters.
  - Removed `SubOnlyButtonColor` (only the old star button used it).
  - `SetNotification` now ignores stale 5 s timers, so a second toast is not hidden early by the first one's timer.
- `INotificationService` / `NotificationService` are unchanged.

## Known limits
- The Search tab no longer shows the video count; the mockup has none.
- Not compiled on the Mac (no SDK). Watch for these on the first Windows build: the FontAwesome name `fa-gear`.

## Merge notes
`MainWindow.axaml` is shared with the other redesign branches. This branch rewrites the header `Border` and the bottom notification `Grid`, and adds `Window.Styles`. The `DataTemplates` and the `MenuCommands` bar are kept as-is, apart from one less indent level. Expect a conflict only if another branch edits the header.

## Added after second test round

- Removed the "Sub-only" button from the header (the user decided it is not needed there).
- Sub-only stays reachable: Settings shows "Sub-only: on/off" and a "Set up" button next to the Application heading. It binds to `MainWindowVM.SubOnlyLabel`/`ShowSubOnlyCommand` through `$parent[Window]`, so the Settings view model is unchanged. In `combined-all` the redesigned Settings page has the same row under General.
