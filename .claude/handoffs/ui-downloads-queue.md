# ui-downloads-queue (redesign branch 3: Downloads queue)

Branch: `claude/ui-downloads-queue-themed` (based on ui-theme-keys). The old `claude/ui-downloads-queue-3f488b` on origin is the pre-theme version; ignore it.

**Depends on:** ui-theme-keys (branch is based on it; merge that first).
 Not compiled here (no .NET SDK on the Mac).

**Test needs:** a quick download (cropped 2-min VODs); one optional check needs a long VOD.

## What changed
- `TwitchVideoDownload`: byte counters, smoothed speed (`SampleSpeed()`, called once a second), estimated size, time left, `FailureReason`, `PauseRequested`, display lines (`StatusChip`, `MetaLine`, `DetailLine`), state bools.
- `DownloadService` / `IDownloadService`: `Enqueue(params, startPaused)`, `Pause(id)`, `Resume(id)`, `MoveUp(id)`, `ClearFinished()`. `Enqueue` no longer drops items while the global pause is on.
  - Pausing a running download cancels it after in-flight chunks; the temp folder is kept and resume skips chunks already on disk. Chunks are written to `.part` then renamed, so a chunk on disk is always complete.
  - Failure reason comes from the stage that was running ("Network error", "Conversion failed; kept .ts", "Disk error: ..."). On conversion failure the joined .ts is moved next to the output.
  - Chunk retries now give up after 3 tries instead of looping forever, so network failures actually surface.
  - Paused items count for `CanShutdown`/`IsFileNameUsed`; removing a paused item deletes its temp folder.
- `DownloadsViewModel` + `DownloadsView.axaml`: summary bar (counts, GB of GB, time left, overall bar, Pause/Resume all, Clear finished), redesigned cards with per-state buttons, empty state. Colours use only the shared theme keys (`{DynamicResource <Key>Brush}`); status chips are a tinted Accent/Success/Warning/Error layer under text in the same colour. No shared files touched.

## Limits
- Sizes are only known after an item fetches its first chunk, so waiting items are left out of the summary totals (`shortcut:` comment in DownloadsViewModel.Refresh).
- Speed is sampled by the view model's 1 s timer, so it only updates once the Downloads view has been opened.
- No "watched searches" pill (later branch).
