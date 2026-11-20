# ui-watched-searches (redesign branch 8: watched searches)

Branch: `claude/ui-watched-searches`, cut from `claude/feature-auto-download-f77d07` with `origin/claude/ui-downloads-queue-themed` merged in.
Written on a Mac with no .NET SDK, so **it has not been compiled**. Build first on the PC.

**Depends on:** auto-download (`claude/feature-auto-download-f77d07`, the base), ui-downloads-queue (`claude/ui-downloads-queue-themed`, merged here: start-paused enqueue, Downloads redesign), ui-theme-keys (comes in through ui-downloads-queue).

**Test needs:** instant UI checks; then "Check now" on a channel that already has VODs (first run, then simulated new videos via the seen file); last and optional, a channel that goes live and publishes a VOD. Script: `test-scripts\test-ui-watched-searches.ps1` (replaces `test-auto-download.ps1`, which tests the old channel-list UI).

```powershell
dotnet build TwitchLeecher\TwitchLeecher\TwitchLeecher.csproj -c Release
powershell -ExecutionPolicy Bypass -File test-scripts\test-ui-watched-searches.ps1 -SkipBuild
```

## What changed

- **Model** `Core/Models/WatchedSearch.cs`: channel, type (Broadcasts/Highlights/Uploads/Clips), min views (clips), quality (`-1` = download default), Active, StartPaused ("When found"). Plain `INotifyPropertyChanged`, not `BindableBase`, because `ReactiveObject` properties don't JSON-serialise. Display helpers (`Label`, `Tags`, `*Index` for ComboBoxes, `LatestResult`, `IsEditing`) are `[JsonIgnore]`.
- **Storage**: `%APPDATA%\Twitch Leecher DX\watched_searches.json` (`Enabled`, `Interval`, `Searches`), owned by `AutoDownloadService` and saved on every change (write to .tmp then rename). It is **not** part of the Settings Save/Undo, so the page applies edits immediately and the Results button can add one directly.
- **Migration** (first start without the json): each channel of the old `<AutoDownload><Channels>` becomes a Broadcasts search, plus a Clips search with the old min views if clips were on; On and interval carry over. `config.xml` is not touched, so rolling back to the auto-download build still works. Seen-list keys stay `name` / `name#clips` (new: `name#highlights`, `name#uploads`), so migrated searches do not run a first check again. An unreadable json is copied to `.bak` before migrating.
- **Service** `Gui/Services/AutoDownloadService.cs` + `Gui/Interfaces/IAutoDownloadService.cs`: per-search check with its own quality and `TryEnqueue(params, startPaused)`. `CheckNow()` wakes the loop (also when switched off); switching On wakes it too. Items get `DownloadParameters.FromWatchedSearch = true`.
  - Log `auto_download.log` is now tab-separated `time  KIND  search  message`, read back by `ReadActivity` for Recent activity and each card's latest result. Kinds: STARTED, PAUSED, WAITING (logged once per live VOD), SKIPPED, FIRST RUN, ERROR. Old-format lines are ignored.
- **Settings page** `Gui/Views/WatchedSearchesView.axaml` + `ViewModels/WatchedSearchesViewModel.cs`: self-contained (own VM, no Preferences binding). Embedded in `PreferencesView.axaml` at outer rows 6-7 (where "Auto Download" was) via `PreferencesViewModel.WatchedSearches`. Inline editor on Edit / "+ Add watched search" (Channel, Type, Min views for clips, Quality, Remove, Done).
  - **Dropping into `claude/ui-settings-sections-v2`:** add `<TabItem Header="Watched searches"><ScrollViewer><views:WatchedSearchesView DataContext="{Binding WatchedSearches}" Margin="..."/></ScrollViewer></TabItem>` after Download, keep the `WatchedSearches` property + constructor parameter in `PreferencesViewModel`, and change `PreferencesView.axaml.cs`'s `ScrollToWatchedSearches` handling to select that tab instead of `BringIntoView`. The view now uses the host page's `section`/`h1`/`label`/`sub` styles (see Restyle below), so it only looks right inside `PreferencesView`.
- **Results**: "Watch this search" in the results title-bar menu (`SearchResultViewModel.WatchSearch`). Channel searches only; switches watched searches on; no duplicates (same channel + type re-activates the existing one). If the results-toolbar branch moves menu commands into a toolbar, move this one with them.
- **Downloads**: pill "● N watched searches" (grey "Watched searches off" when off; hidden with no searches) → `INavigationService.ShowWatchedSearches()` opens Settings scrolled to the section. Chips get " · WATCHED"; a paused watched item says "Found by a watched search, waiting for you".
- **Merge of ui-downloads-queue** (conflicts resolved in `DownloadService.cs`): kept combined's clip download and piped conversion with its `.ts` fallback, added the queue branch's stage names, `.part` chunk writes and resume-skip. When the fallback keeps a `.ts`, the stage becomes "Conversion failed; kept .ts" (the queue branch's `KeepConcatFile` only runs on its own path). `DownloadsView.axaml` taken from the queue branch, with combined's `x:Name="Scroller"`.

## Restyle to match the Settings page (note 7)

The section was dark nested cards and didn't look like the other Settings sections. `WatchedSearchesView.axaml` now:
- **Before:** its own 20px heading with an "On" tick on the right; status in a rounded `CardBackground` box; each search a rounded `CardBackground` card with 8px gaps; activity in a rounded `PanelBackground` box; 11px "When found" and tag text; host margin `24,16`.
- **After:** `StackPanel.section` + `h1` like General/Search/Download (same 24,20 margin, 18 spacing, 640 max width). The master switch is a checkbox row "Check watched searches for new videos" with the "Checks only run while…" line as its `sub`. The status line is a plain row (dot, text, Every, Check now) with no box. SEARCHES / RECENT ACTIVITY use `label`. Searches and activity are flat rows with a 1px `DividerBrush` line under each (and one above the list), no background; `CardBackgroundBrush` only on hover and on the row whose editor is open. Rows sit 10px out (`Margin="-10,0"`) so their text lines up with the headings. Inactive rows fade their content, not the hover background. Small text is 12.
- Unchanged: When-found dropdown, Edit / inline editor, Active, Check now, interval, "+ Add watched search", activity list, empty-state text.
- Colours checked against New, Original and Coyote values: in all three Card is one step lighter than App and Divider one step lighter than Card, so hover rows, divider lines, tag chips (Divider) and the `ws` button hover (Divider) stay visible. Not seen running (no build here).
 (`// shortcut:` in code where relevant)

- Latest result per card is matched by label, so renaming a channel shows "Nothing found yet" until its next event.
- A skipped video is marked seen and not retried; seen ids are never pruned (both carried over from auto-download).
- No avatars on the cards (mockup has gradient circles; no avatar data and no hex colours allowed).
- The mockup's "Turn on minimise to tray" link is left out: the tray setting lives on the tray-notifications branch. Add the link once both are merged.
- Checks only run while the app is open; "Last check" is in memory, so after a restart it says "Not checked yet" until the start-up check.
- Old `AutoDownload*` preferences and their XML stay (read for migration only). Remove them in a later cleanup once nobody needs to roll back.

## Unverified (needs the PC build)

- `DataTemplate x:DataType="x:String"` for the tag chips.
- ComboBoxes with inline `ComboBoxItem`s bound by `SelectedIndex` to `WatchedSearch.*Index`.
- `IsVisible="{Binding WatchedSearchesText, Converter={x:Static ObjectConverters.IsNotNull}}"` on the Downloads pill.
- Settings scroll-to-section timing against `ScrollPositionRestorer` (posted at Background priority).

## Added after testing

- **Live at setup:** if the channel is live when a watched search first runs, that stream is no longer marked as seen. Its VOD downloads (or waits paused) once the stream ends. The FIRST RUN log line says so.
