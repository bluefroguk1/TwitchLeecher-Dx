# feature-tray-notifications: first-minimise tray prompt

Branch: `claude/feature-tray-notifications-963477` (adds to the existing tray branch).

## What changed
- New preference `AppTrayPromptShown` (default false): `Preferences.cs` (field, property, `Clone`), and `PreferencesService` (save, load, defaults). It's stored as `<TrayPromptShown>` under `<Application>` in `config.xml`.
- New `Views/TrayPromptWindow.axaml(.cs)`, a small dialog:
  - "Minimise to the system tray instead?";
  - "Yes, use the tray" / "No, keep it on the taskbar";
  - "Don't ask again", ticked by default.
- The dialog uses theme brushes only: `AppBackgroundBrush`, `TextPrimaryBrush`, `TextMutedBrush`, `AccentBrush` and `AccentForegroundBrush`. These come from `ui-theme-keys`. On this branch alone they don't resolve, so the dialog falls back to SimpleTheme defaults. They resolve in `combined-all`.
- `MainWindow.axaml.cs`: when the window is minimised, tray is off and `!AppTrayPromptShown`, the minimise happens first. Then the dialog opens (posted to the UI thread, no owner, `Topmost`, centred on screen).
  - **Yes** sets `AppMinimiseToTray = true` and hides the window to the tray.
  - **No** sets `AppMinimiseToTray = false`.
  - `AppTrayPromptShown` is set to the checkbox value, then the preferences are saved.
  - Closing the dialog with X saves nothing, so the question comes back on the next minimise.

## Known limits
- If the Settings page is open with unsaved edits when the prompt is answered, saving Settings afterwards overwrites the answer with what the page shows. The page loads a copy of the preferences when it opens.

## Test
- `test-scripts/test-tray-notifications.ps1`: new section 0. It resets `TrayPromptShown`/`MinimiseToTray` in `config.xml` first.
- Test needs: Windows desktop with the app started fresh; just minimise the window.
