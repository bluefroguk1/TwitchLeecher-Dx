# ui-search-sidebar: redesigned Search screen (branch 4)

**Branch:** `claude/ui-search-sidebar-93487b` (on `ui-theme-keys`, from `master`)
**Depends on:** ui-theme-keys
**Test needs:** instant (UI only) for layout; a quick search needs only sign-in (no download)
**Test script:** `test-scripts/test-ui-search-sidebar.ps1`

## What changed
- `SearchView.axaml` rewritten to match `MainAfter.dc.html`: 300px sidebar (Channel | Links / IDs switch, channel field with ★ dropdown + favourite chips + "All favourites", Type chips, Show dropdown, Search button, Enter to search) and an empty state (logo, product + version, tagline, Recent searches).
- `SearchViewModel`: `IsLinksMode`, `LinksText`, `ShowIndex` (Last N / Today / Last 7 days / Custom), `PickFavourite`, `SearchFavourites`, `RunRecentSearch`. Links box: all-numeric lines -> Ids search, otherwise IDs become video links -> Urls search. Clear commands removed.
- Recent searches: `RuntimeData.RecentSearches` saved to `runtime.xml` (`SearchParameters.GetXml/GetFromXml`), added in `SearchService.PerformSearch`, last 5, de-duplicated.
- Welcome page removed (`WelcomeView`, `WelcomeViewModel`, `ShowWelcome`); app opens on Search, "Search on Start" still runs the default search. Unused `SearchTypeToBooleanConverter` deleted.

## Known limits
- Not compiled (no SDK on the Mac). Watch: `KeyBinding` in `UserControl.KeyBindings`, `sys:String` DataTemplate, RadioButton chip template.
- `shortcut:` favourites search is date-range only, so "Last N videos" uses the Settings default range.
- `shortcut:` "Last 7 days" recent searches store absolute dates.
- Inputs keep their current look; dark inputs come from `ui-dark-inputs`.
- Colours use only the shared `{DynamicResource <Key>Brush}` keys; text on the accent uses `AccentForegroundBrush`. The selected Type chip is card background + accent border (no tinted-accent key exists).

## Merge notes
- `MainWindow.axaml`: only the WelcomeView DataTemplate is removed. `ui-header-nav` re-indents that block, so resolve by dropping the Welcome template.
- `MainWindowVM.cs`: `ShowWelcomeOrSearch` became `ShowSearchOnStart`.
