# Handoff: ui-settings-sections (UI redesign, branch 7: Settings page)

Branch `claude/ui-settings-sections-v2`, cut from `combined` (supersedes `claude/ui-settings-sections-c7c12e`, which lacks the theme keys). Written on a Mac without the .NET SDK, so **it has not been compiled yet**: build first on the PC.

**Test needs:** instant (UI only). Script: `test-scripts\test-ui-settings-sections.ps1`.

**Depends on:** `ui-theme-keys` (commit f001555, cherry-picked here as the first commit; identical change, merges cleanly), #96 (quick download setting `DownloadQuickDownload`) and #86 (favourites default `SearchDefaultFavourites`). Both are in `combined`. Rebase onto `master` once those merge upstream, then open the upstream PR.

## What changed

Only three files: `TwitchLeecher.Gui/Views/PreferencesView.axaml` (+ `.axaml.cs`) and `TwitchLeecher.Gui/ViewModels/PreferencesViewModel.cs`. This branch's own commits touch no shared files; the `App.axaml` change is only the cherry-picked `ui-theme-keys` commit. Every colour is a `{DynamicResource <Key>Brush}` from those keys (no hex), text on the accent uses `AccentForegroundBrush`, and the inputs on this page use the `Input*` brushes.

- **Side menu** = a `TabControl` with `TabStripPlacement="Left"` (`x:Name="Sections"`), one `TabItem` per section: General, Search, Download, Appearance. One section shows at a time; the VM keeps `SelectedSectionIndex` so the page reopens where it was left.
  - **Adding a section** (e.g. "Watched searches", "Notifications & tray"): add one `<TabItem Header="Watched searches">` with a `ScrollViewer` > `StackPanel Classes="section"` inside, at the position it should appear in the menu. Styling comes from the existing `TabControl#Sections TabItem` and `.section/.field/.label/.sub` styles in the view. Nothing else to register.
  - Old "Miscellaneous" (external player) went to General; old "Experimental" (max connections) went into Download's Advanced expander next to the temporary folder.
- **Download section** matches `SettingsAfter.dc.html`: folder + Browse, favourites subfolder, file name template with insert chips (insert at the caret, replacing a selection; code-behind `InsertWildcard`), "All wildcards…" flyout (replaces the big tooltip table; every row is clickable), live preview via `IFilenameService.SubstituteWildcards` + `EnsureExtension` on a static sample video, default quality, three plain-English ticks with sub-text, "Advanced: temporary folder and connections" expander.
- **Save bar** at the bottom, visible only while `HasChanges`: "You have unsaved changes", Reset to defaults, Undo, Save. The title-bar Save/Undo/Default menu commands are gone for this page (`BuildMenu` override removed).
  - Validation unchanged (`Preferences.Validate`), and the errors are also listed in red in the save bar because the bad field may sit in a hidden section. After a failed save the message updates live as you fix things.
  - **Behaviour change:** Reset to defaults now loads the defaults as unsaved changes (Undo drops them, Save keeps them) instead of saving them straight away. A "Reset all settings to defaults…" link in General makes it reachable when nothing has changed yet.
  - Undo still asks for confirmation (existing dialog).

## Known limits (marked `// shortcut:` where in code)

- `HasChanges` compares the edited copy with the saved preferences (null and empty text count as equal).
- Toggles are `CheckBox`es with the label on the right of the box, not right-aligned switches as in the mockup; that keeps the whole label clickable.
- Leaving the Settings page still discards unsaved changes (old behaviour: `OnBeforeHidden`).
- The scroll position is no longer restored for this page (each section scrolls on its own and is short). `PreferencesViewModel` no longer implements `INavigationState`.

## Unverified (needs the PC build)

- The SimpleTheme `TabItem` template: the selected-item background style targets `/template/ ContentPresenter#PART_ContentPresenter`. If the selected item doesn't highlight, check the template part name.
- `Run Text="{Binding FilenamePreview}"` in a `TextBlock` (compiled binding).
