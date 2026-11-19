# Handoff: TwitchLeecher-Dx (master overview)

Updated 2026-11-19. This is the **overview** for Claude on the Windows PC. Each feature branch also has its own detailed `.claude/handoffs/LATEST.md`. Read that one when you check out the branch.

The work was written on a Mac with no .NET SDK. The PC has the SDK: your job is to **build, test with the user, fix, and push when asked**.

## Rules (from the user, keep them)

- **Never name, link or credit the third-party Twitch downloader project used for ideas.** This covers code, comments, commit messages, PR text and docs.
- Commit freely on feature branches. **Push only when the user asks.** Never commit to `master`.
- End commit messages with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
- Mark known limits in code as `// shortcut: <limit>, <when to upgrade>`.
- Ask before merging branches into each other or opening PRs to upstream.

## Project

- **Stack:** C# .NET 10, Avalonia 11.3, Ninject DI, CommunityToolkit.Mvvm, `IEventAggregator`. The solution is in `TwitchLeecher/`. The app project is `TwitchLeecher/TwitchLeecher/TwitchLeecher.csproj`. Output goes to `bin\Release\net10.0`.
- **Running it:** launch `TwitchLeecher.exe`, not `dotnet TwitchLeecher.dll`. The dll launch shows up as ".NET Host" with the .NET icon.
- **Repos:**
  - Fork: `bluefroguk1/TwitchLeecher-Dx`.
  - Upstream: `schneidermanuel/TwitchLeecher-Dx`. The user has **16 PRs open there** (#104–#119). Nobody knows when the upstream developer will review them.

## Branch map

| Branch | What it is |
|---|---|
| `master` | Clean copy of upstream. Leave it alone. |
| 16 × `*-pr` | One per open upstream PR. Deleting a branch closes its PR. Fix PR feedback on these. |
| `test-scripts` | Manual test scripts for all 16 PR branches (`test-scripts\test-<branch>.ps1`) |
| **`combined`** | **All 16 PRs merged, with conflicts resolved. Use this one day to day.** |
| `claude/ci-build-41fa8a` | GitHub Actions build. **First run passed** on `combined` + workflow, so `combined` compiles. |
| `claude/login-page-and-icon-8b7b57` | Friendly "Signed in" browser page; the app comes to the front after sign-in |
| `claude/fix-slow-downloads-15ccca` | Faster chunk downloads (`Parallel.ForEachAsync`, full connection count) |
| `claude/feature-tray-notifications-963477` | Tray icon, minimise to tray, desktop notifications (DesktopNotifications 1.3.1) |
| `claude/feature-auto-download-f77d07` | Watches channels and auto-downloads new VODs, and clips if enabled |
| `claude/feature-new-theme-645696` | New "Eye Stripe" colour theme, designed with the user (mockup: https://claude.ai/artifact/R6MAtnxzoDPGSpbnY3PnxG) |

All `claude/*` branches are cut from `combined`, one commit or so each, and **none of them except CI has been compiled yet**.

### What `combined` resolved (in case a PR changes)

- **Direct-to-ffmpeg + #49 (keep `.ts`):** if conversion fails on the piped path, the chunks are joined into a `.ts` before cleanup.
- **HEVC (#48) + size estimate (#35):** `TwitchVideoQuality(id, name, resolution, codecs, bandwidth)`.
- **Clips (#37) + quick download (#96):** `CreateDownloadParams` handles clips. The chat checkbox is hidden for clips. VOD info is also saved for clips. The size estimate shows "unknown" for clips.
- **Scroll fixes:** all pages use the shared `ScrollPositionRestorer`.

## Suggested order on the PC

1. **CI first.** Merge `claude/ci-build-41fa8a` into `combined` (a fast-forward plus one file). After that, every branch cut from `combined` gets a ready-to-run Windows zip on each push, under GitHub Actions → run → Artifacts. Only branches that *contain* the workflow file get built. Ask the user before merging.
2. **Build and test each `claude/*` branch** with its script: `powershell -ExecutionPolicy Bypass -File test-scripts\<script>.ps1`.
   - Each script writes results to `%TEMP%`.
   - Fix build errors first. Each branch's handoff lists the most likely compile errors, because the code was written without a compiler.
   - Recommended order:
     1. `fix-slow-downloads` (`test-slow-downloads.ps1`, times master against the branch)
     2. `login-page-and-icon` (`test-login-and-icon.ps1`)
     3. `feature-tray-notifications` (`test-tray-notifications.ps1`)
     4. `feature-auto-download` (`test-auto-download.ps1`)
     5. `feature-new-theme` (`test-new-theme.ps1`)
3. **Then fold the passing ones into `combined`.** Ask the user first. Expected conflicts:
   - **login × tray:** 2 hunks in `MainWindow.axaml.cs`. Both made a `BringToFront()` method; keep one. The tray version also calls `Show()`.
   - **auto-download × tray × theme:** `PreferencesView.axaml` row numbers (each adds Settings rows) and `Preferences.cs`/`PreferencesService.cs`.
   - **`.claude/handoffs/LATEST.md`** conflicts on every merge: keep this overview, and drop the branch's handoff once that branch is merged.
   - After tray and auto-download are both in, wire `IDesktopNotificationService.Notify(...)` at the `// DESKTOP NOTIFICATION:` line in `AutoDownloadService.CheckVideos`.

## Known bugs not yet fixed

- **Single-instance pipe name mismatch:** `App.axaml.cs` sends "Activate" on pipe `TwitchLeecher`, but `MainWindow` listens on `TwitchLeecher-DX`. A second launch probably doesn't bring the running window (or the tray-hidden window) forward.
- **Window flash at launch:** on the login branch, a saved sub-only token publishes `SubOnlyAuthChangedEvent(true)` at startup, so the window may briefly go Topmost at launch.
- **macOS isn't supported:** the app expects `ffmpeg.exe`, "open folder" does nothing, and there's no notification backend.

## Remaining ideas (not started)

- #42: preview on hover.
- #31: full async rewrite. The slow-downloads branch made the chunk loop async; `WebClient` and `Thread.Sleep` remain elsewhere.
- #36: CLI.
- Per-theme fonts and corner radius, and user-loadable theme files.
- #85 is already implemented and can be closed upstream; #25 and #38 can't be reproduced.
