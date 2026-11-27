# ui-results-toolbar (UI redesign branch 5: search results)

Branch: `claude/ui-results-toolbar-themed` (from `combined`). The older `claude/ui-results-toolbar-498b08` is superseded (hex colours, no theme keys).
Depends on: ui-theme-keys (first commit here is a cherry-pick of f001555), #96 (selection, Download Selected), #37 (clips). Rebase onto `master` once those merge, dropping the theme-keys cherry-pick if ui-theme-keys is already in; the other commits are self-contained (model, then view+VM, then tests/docs).

Test needs: instant (UI only) for layout/sort/toggle/select; a quick download for QUEUED/DOWNLOADED badges.

## What changed
- `TwitchVideo`: `IsQueued`, `IsDownloaded`, `ClipCreator`, `MetaStr`. `ApiService.ParseClip` reads `creator_name`.
- `SearchResultViewModel`: search summary, Hide downloaded, sort (newest/oldest/longest/most viewed), grid/list, Select all, "N selected · ~X GB", "Download N videos" (reuses DownloadSelected). Default path logic shared via `GetDefaultTarget`.
- `SearchResultView.axaml`: rewritten to match `ResultsAfter.dc.html` (no "Watch this search" — later branch). Colours come only from the theme brushes (`{DynamicResource <Key>Brush}`); only the translucent black thumbnail overlays are fixed.

## Not verified
- Never compiled (no .NET SDK on the Mac). Build on Windows first; fix any XAML/compile errors.
- Shortcuts: DOWNLOADED only detects default-named files and not when the filename uses `{res}`; size estimate makes 2 API calls per ticked video; QUEUED badge refreshes on queue add/remove and when the page is shown, not when a download finishes while you watch.

## Added after testing
- Channel header: for channel searches the summary bar shows the Twitch display name, the round avatar (Helix `users` `profile_image_url` via new `IApiService.GetChannelInfo`, loaded with `ImageHelper`) and a ★ toggle. Hidden for Links / IDs and Favourites searches.
- ★ (`ToggleFavouriteCommand`) adds/removes the channel in `Preferences.SearchFavouriteChannels`, saves preferences and notifies "Added to / Removed from favourites". Outline star = not a favourite, solid accent star = favourite.
- `SearchResultViewModel` now keeps its own `_resultParams` (the search the results came from) because `SearchService` resets `LastSearchParams` on every preferences save; without it the ★ would flip the summary to the default search.
- Not compiled. The header costs one extra Helix `users` call per channel search.
