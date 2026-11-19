# ui-results-toolbar (UI redesign branch 5: search results)

Branch: `claude/ui-results-toolbar-498b08` (from `combined`).
Depends on: #96 (selection, Download Selected), #37 (clips). Rebase onto `master` once those merge; commits are self-contained (model, then view+VM, then tests/docs).

Test needs: instant (UI only) for layout/sort/toggle/select; a quick download for QUEUED/DOWNLOADED badges.

## What changed
- `TwitchVideo`: `IsQueued`, `IsDownloaded`, `ClipCreator`, `MetaStr`. `ApiService.ParseClip` reads `creator_name`.
- `SearchResultViewModel`: search summary, Hide downloaded, sort (newest/oldest/longest/most viewed), grid/list, Select all, "N selected · ~X GB", "Download N videos" (reuses DownloadSelected). Default path logic shared via `GetDefaultTarget`.
- `SearchResultView.axaml`: rewritten to match `ResultsAfter.dc.html` (no "Watch this search" — later branch).

## Not verified
- Never compiled (no .NET SDK on the Mac). Build on Windows first; fix any XAML/compile errors.
- Shortcuts: DOWNLOADED only detects default-named files and not when the filename uses `{res}`; size estimate makes 2 API calls per ticked video; QUEUED badge refreshes on queue add/remove and when the page is shown, not when a download finishes while you watch.
