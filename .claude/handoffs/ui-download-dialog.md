# UI redesign 6: Download dialog

Branch: `claude/ui-download-dialog-f97eb4` (from `combined`). Matches `DownloadAfter.dc.html`.

**Test needs:** a quick download (all checks but the last are instant UI; the last is a 1-minute cropped download).
Script: `test-scripts/test-ui-download-dialog.ps1`.

**Depends on:** ui-theme-keys (commit f001555, cherry-picked as this branch's first commit), and open PRs #35 (size estimate), #50 (VOD info), #71 (chat), #37 (clips). Rebase onto `master` once they merge.

## What changed
- `Views/DownloadView.axaml`: rewritten as a card. Header (thumbnail, title, "channel · date · length"), Quality | Save to (Change), always-editable file name with ↺ reset, Part to download, muted banner, Extras, footer Cancel + Download.
- `Controls/RangeStrip.cs` (new): draws the filmstrip, dims outside the selection, two draggable handles (min 5 s apart, the existing crop rule). With no frames it is a plain range bar.
- `ViewModels/DownloadViewModel.cs`:
  - the H/M/S crop properties and the "Use Custom Filename" checkbox are replaced by `RangeStart/RangeEnd` (seconds, for the strip) and `RangeStartText/RangeEndText` (HH:MM:SS fields; a bad value throws, which shows as a field error);
  - a range at 0 / at full length maps to `CropStart/CropEnd = false`, so `DownloadParameters` and the download path are unchanged;
  - the file name follows the template until the user edits it, and ↺ resets it;
  - the Download/Cancel menu entries are removed (buttons are in the footer now);
  - the muted banner shows whenever the VOD is muted (before: only with conversion on); the "converion" typo is gone.
- `IApiService.GetStoryboard` / `ApiService` + `Models/TwitchStoryboard.cs`: fetches the seek-preview storyboard.

## Storyboard (verified live with curl, 2026-11-19)
- GQL `query{video(id:"<id>"){seekPreviewsURL}}` with `Client-ID: Constants.ClientIdWeb` returns
  `https://<cloudfront>/<...>/storyboards/<id>-info.json`; an unknown id returns `video: null`.
- That JSON is an array of two boards:
  `{"count":200,"width":160,"height":90,"cols":5,"rows":40,"interval":16,"quality":"low","images":["<id>-low-0.jpg"]}`
  and `{"count":200,"width":220,"height":124,"cols":5,"rows":10,"interval":16,"quality":"high","images":["<id>-high-0.jpg", ... "-high-3.jpg"]}`.
- `count` is always 200 (padded); `interval` scales with length (16 s for a 50 min VOD, 168 s for 9 h). Image names are relative to the JSON URL.
- The app uses the `high` board. It is fetched once when the dialog opens (only when cropping is possible), all sheets are loaded with `ImageHelper.LoadFromWeb`, and frames are cut with `CroppedBitmap`. The CDN request uses a plain `WebClient`, so the OAuth header is not sent there.

## Known limits
- `// shortcut:` previews are storyboard stills (one every 16 s to a few minutes), not exact frames.
- All colours use the shared theme keys (`{DynamicResource <Key>Brush}`); the only fixed colours are translucent overlays (dimmed strip area, timecode chips). `RangeStrip` takes its brushes as properties set from XAML, so a theme switch re-renders it.
- The file name keeps its extension in the text box (the mockup shows `.mp4` outside it) so `.mkv` stays selectable.
- Not compiled here (no .NET SDK on the Mac); written against Avalonia 11.3 APIs. `App.axaml` changes come only from the cherry-picked theme-keys commit; `MainWindow.axaml` is untouched.
