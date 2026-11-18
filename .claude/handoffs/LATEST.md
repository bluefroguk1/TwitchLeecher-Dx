# Handoff: TwitchLeecher-Dx fixes and features

Written 2026-11-18 on a Mac, where nothing could be compiled or run (no .NET SDK). This continues on a Windows PC, which can build and test.

## Start here (for Claude on the PC)

1. Nothing below has been compiled yet. For each branch: check it out, run its test script, and fix any build errors on that branch.
2. Test in this order (riskiest first). Each script builds the app, launches it, walks the user through checks and writes a results file to `%TEMP%`:

   | # | Branch | Script |
   |---|---|---|
   | 1 | `bugfixes` | `test-scripts\test-bugfixes.ps1` |
   | 2 | `feature-48-hevc` | `test-scripts\test-feature-48-hevc.ps1` |
   | 3 | `feature-37-clips` | `test-scripts\test-feature-37-clips.ps1` |
   | 4 | `feature-71-chat` | `test-scripts\test-feature-71-chat.ps1` |
   | 5 | `feature-96-qol` | `test-scripts\test-feature-96-qol.ps1` |
   | 6 | `feature-requests` | `test-scripts\test-feature-requests.ps1` |

   Run each with: `powershell -ExecutionPolicy Bypass -File test-scripts\<script>.ps1` (add `-SkipBuild` to reuse a build). The scripts ask y/n/s questions, so the user has to answer the manual checks.
3. Every branch is cut from `master` (a16111c), so each one is tested on its own. A fix in one branch is not in the others.

## Rules

- **Never name, link or credit the third-party Twitch downloader project we looked at for ideas.** This applies to code, comments, commit messages, PR text and this repo's docs.
- Commit and push only when the user asks. Work on the feature branch, never `master`.
- Mark known limits in code as `// shortcut: <limit>, <when to upgrade>`.

## Project facts

- C# .NET 10, Avalonia 11.3, Ninject DI, CommunityToolkit.Mvvm. Solution in `TwitchLeecher/`. Build output: `TwitchLeecher/TwitchLeecher/bin/Release/net10.0`; launch with `dotnet TwitchLeecher.dll`.
- ffmpeg: on Windows, `Libraries\ffmpeg.exe` is copied next to the app by the build. On Linux it comes from PATH. **macOS is not supported**: the app looks for `ffmpeg.exe` and "open folder" does nothing there.
- Twitch APIs used (in `TwitchLeecher.Services/Services/ApiService.cs`):
  - Helix (`/videos`, `/users`, `/clips`) with the user's OAuth token.
  - GQL at `gql.twitch.tv/gql` with the web Client-ID (`Constants.ClientIdWeb`), using persisted queries.
  - The usher VOD playlist.

## Branches (all pushed to origin)

### `bugfixes` (e263f1d): resource use and reported bugs
- `DownloadService`:
  - Progress timer goes from every 2 ms to every 1000 ms.
  - Uses one shared `HttpClient` instead of a new connection per chunk.
  - Chunk retries are now limited, so they no longer loop forever.
  - `warnings` is a `ConcurrentBag`.
  - Parallelism is at least 1.
- `ProcessingService`: the merge uses `Stream.CopyTo`; ffmpeg runs with `-nostdin` (#92).
- `ImageHelper`: shared HttpClient, and responses are disposed.
- #87: the window position is restored correctly and only when that point is on a connected screen (`MainWindow.axaml.cs`).
- #103: tooltips wrap; the time inputs are wider; the "Disable conversion" label wraps.
- #56: the channel name is trimmed before searching.
- The update link and the "open folder" path both work when the path has spaces.

### `feature-requests` (851ff30): #49
If ffmpeg conversion fails, the unconverted video is kept as `.ts` instead of being deleted. The test script fakes a broken ffmpeg and restores the real one afterwards.

### `feature-48-hevc` (c826c07): #48 HEVC/AV1
- Uses the v2 usher playlist with `supported_codecs=av1,h265,h264`.
- The parser reads `STABLE-VARIANT-ID`/`IVS-NAME` and falls back to the old `EXT-X-MEDIA` format.
- Quality names get " HEVC" or " AV1" added.
- 1440p HEVC only appears when **logged in** (Twitch returns it as unavailable otherwise).

### `feature-96-qol` (7248956): #96
- New "Quick download" setting: skips the download dialog and uses the defaults.
- A checkbox on each search result, plus "Download Selected" in the menu. Videos that can't be added are listed in a message box.
- The results list now keeps its scroll position when you come back to it.

### `feature-37-clips` (10611c2): #37 clips
- A "Clip" search type for channels and favourites (Helix `/clips`, date range, paging).
- Clip URLs (`twitch.tv/<channel>/clip/<slug>` and `clips.twitch.tv/<slug>`) work in URL search.
- Download uses the GQL `VideoAccessToken_Clip` query to get a signed mp4, which is saved directly. Cropping and conversion are not offered for clips.
- shortcut: the clip resolution is assumed to be 16:9.

### `feature-71-chat` (1760a51): #71 chat as text
- "Save chat (.chat.txt)" checkbox in the download dialog.
- After the video finishes, the app writes `<video>.chat.txt` with lines `[HH:MM:SS] name: text`. Times are measured from the crop start.
- Chat is fetched with the GQL `VideoCommentsByOffsetOrCursor` query, **paging by time offset only**: cursor paging now fails with `IntegrityCheckFailed`.
- shortcut: if more than one page of messages falls in the same second, the overflow is lost.
- If saving chat fails, it becomes a warning and the video is kept.

### Merge conflicts between branches
- `feature-37-clips` conflicts with `48` (1 hunk), `96` (2) and `71-chat` (3).
- No other pairs conflict.
- An integration branch combining everything has been offered but not built.

## Still to do (not started)

**Bugs found in the code:**
- `SearchService`: after a search fails, the code still reads `task.Result`. The error is shown, then it throws and still navigates to the results page. It needs an early return.
- The Downloads and Settings pages probably reset to the top when you return to them (same bug as #96).
- macOS support: use `ffmpeg` without `.exe` and open folders with `open`.

**Open issues:**
- #50: save VOD info to a file.
- #86: Favourites as the default search.
- #35: estimated file size (bandwidth × length).
- #42: preview on hover.
- #31: async rewrite.
- #36: CLI.
- #85 is already done and can be closed.
- #25/#38 are old and can't be reproduced.

**Optimisations, biggest first:**
1. Send the chunk list straight to ffmpeg instead of writing a merged `.ts` first. This saves one full disk write and about 2× peak disk use. Check the newer streams that use an init segment first.
2. `ApiService.WaitForNextRequest` sleeps until the rate-limit reset after every request. It should only wait when the remaining count is 0.
3. The download log rebuilds its whole string on every line; append instead.
4. All thumbnails start downloading at once; load them lazily.
5. Replace `WebClient` with async `HttpClient` throughout (#31).
6. Add a GitHub Actions build for every branch so compile errors show up before testing.
