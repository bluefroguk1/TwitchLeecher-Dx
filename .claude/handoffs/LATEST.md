# Handoff: TwitchLeecher-Dx (master overview)

Updated 2026-11-19 (evening). This is the **overview** for Claude on the Windows PC. Every branch below also carries its own detailed handoff in `.claude/handoffs/`. The UI branches name theirs `.claude/handoffs/<name>.md`; the older feature branches use `LATEST.md`. Read it after you check out the branch.

Everything was written on a Mac with no .NET SDK, so **nothing below has been compiled except `combined`**, which passed the GitHub Actions build. Your job on the PC is to build, test with the user, fix, and push when asked.

## Rules (from the user, keep them)

- **Never name, link or credit the third-party Twitch downloader project used for ideas.** This covers code, comments, commit messages, PR text and docs.
- Commit freely on feature branches. **Push only when the user asks.** Never commit to `master`.
- End commit messages with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
- Mark known limits in code as `// shortcut: <limit>, <when to upgrade>`.
- Ask before merging branches into each other or opening PRs to upstream.
- **Each change is its own branch and its own upstream PR**, so the developer can pick and choose. Don't bundle changes.
- **Views use theme brushes only:** `{DynamicResource <Key>Brush}` from `ui-theme-keys`, never hex colours. The only exception is translucent black overlays. Every theme (New, Original, Coyote) must define all 17 keys **twice: as a `...Color` and as a `...Brush` with the literal colour.** A brush whose colour is a `DynamicResource` renders transparent after a theme switch (found in testing, fixed on every redesign branch).

## Project

- **Stack:** C# .NET 10 (LTS; stay on it, .NET 11 adds nothing needed), Avalonia 11.3, Ninject, CommunityToolkit.Mvvm. The solution is in `TwitchLeecher/`; the app project is `TwitchLeecher/TwitchLeecher/TwitchLeecher.csproj`.
- **Running it:** launch `TwitchLeecher.exe`, not `dotnet TwitchLeecher.dll`. The dll launch shows up as ".NET Host" with the .NET icon.
- **Repos:**
  - Fork: `bluefroguk1/TwitchLeecher-Dx`.
  - Upstream: `schneidermanuel/TwitchLeecher-Dx`, where the user has **16 PRs open** (#104–#119).
- **Design canvas** (approved before/after mockups of every screen): https://claude.ai/artifact/CYBdwo46oR2LqydWnUdo62

## Branch map

| Branch | What it is | Starts from | Depends on |
|---|---|---|---|
| `master` | Clean copy of upstream. Leave it alone. | — | — |
| 16 × `*-pr` | One per open upstream PR. Fix PR feedback here. | `master` | — |
| `test-scripts` | Test scripts for the 16 PR branches | — | — |
| **`combined`** | All 16 PRs merged. The user's daily build. | `master` | — |
| `claude/ci-build-41fa8a` | GitHub Actions build plus a Windows zip artifact. Passed. | `combined` | — |
| `claude/login-page-and-icon-8b7b57` | Friendly sign-in page; the app comes to the front after sign-in | `combined` | — |
| `claude/fix-slow-downloads-15ccca` | Faster chunk downloads (`Parallel.ForEachAsync`) | `combined` | — |
| `claude/feature-tray-notifications-963477` | Tray icon, minimise to tray, desktop notifications | `combined` | — |
| `claude/feature-new-theme-645696` | "Coyote" theme, including values for the redesign keys | `combined` | ui-theme-keys (picked in) |
| `claude/feature-auto-download-f77d07` | Channel monitoring and auto-download (older UI) | `combined` | — |
| **UI redesign** | | | |
| `ui-theme-keys` | 17 shared colour keys and brushes for New and Original. **Its PR goes first.** | `master` | — |
| `claude/ui-dark-inputs-0028fd` | Dark form controls and a 12/14/16/20/24 type scale | ui-theme-keys | ui-theme-keys |
| `claude/ui-header-nav-d65209` | Labelled tabs, Settings button, account menu, corner toasts | ui-theme-keys | ui-theme-keys |
| `claude/ui-search-sidebar-93487b` | Sidebar search, merged Links/IDs, recent searches; **opens on Search** (Welcome page removed) | ui-theme-keys | ui-theme-keys |
| `claude/ui-downloads-queue-themed` | Speed and time left, summary bar, **per-download pause/resume**, Clear finished, failure reasons | ui-theme-keys | ui-theme-keys |
| `claude/ui-results-toolbar-themed` | Sort, filter, grid/list, badges, Select all | `combined` | ui-theme-keys, #96, #37 |
| `claude/ui-download-dialog-themed` | Thumbnail, **crop preview frames and filmstrip**, editable filename | `combined` | ui-theme-keys, #35, #50, #71, #37 |
| `claude/ui-settings-sections-v2` | Settings side menu, wildcard chips, filename preview, save bar | `combined` | ui-theme-keys, #96, #86 |
| `claude/ui-watched-searches` | Auto-download reworked as "watched searches", per-search "Download now / Add paused", Downloads pill, "Watch this search" | auto-download branch + downloads-queue merged in | auto-download, ui-downloads-queue, ui-theme-keys |
| `claude/in-app-signin` | Sign in to Twitch inside the app (web view); optional "Also enable sub-only downloads" in the same login; tokens in protected storage | `master` (+ theme keys picked in) | ui-theme-keys; conflicts with login-page-and-icon in `AuthListener.cs` |

The branches that start from `combined` must be **rebased onto `master` once their upstream PRs merge**, before their own PR is opened. Their handoffs say how.

## Testing order: quickest first, so nothing simple waits behind something slow

For each branch, check it out and run its script:

```
git fetch origin
git checkout <branch>
powershell -ExecutionPolicy Bypass -File test-scripts\<script>.ps1
```

**First check that it builds** (`dotnet build TwitchLeecher\TwitchLeecher\TwitchLeecher.csproj -c Release`). Each branch's handoff lists its most likely compile errors. Each script writes results to `%TEMP%`; ask the user to paste them, then fix the FAILs.

### Tier 1: instant, UI only (sign-in at most, no downloads)

| # | Branch | Script | Notes |
|---|---|---|---|
| 1 | `claude/ui-dark-inputs-0028fd` | `test-ui-dark-inputs.ps1` | It contains ui-theme-keys, so this also tests the keys. Switch New ↔ Original. |
| 2 | `claude/ui-header-nav-d65209` | `test-ui-header-nav.ps1` | Tabs, account menu, toast, theme switch |
| 3 | `claude/ui-settings-sections-v2` | `test-ui-settings-sections.ps1` | Side menu, chips, preview, save bar |
| 4 | `claude/feature-new-theme-645696` | `test-new-theme.ps1` | Coyote theme; it previously crashed on switch (fixed in ccb19e8) |
| 5 | `claude/ui-search-sidebar-93487b` | `test-ui-search-sidebar.ps1` | Opens on Search; a search needs sign-in only, no download |
| 6 | `claude/ui-results-toolbar-themed` | `test-ui-results-toolbar.ps1` | Run the layout, sort, toggle and select steps now. Its QUEUED/DOWNLOADED badge steps need a quick download, so skip them until Tier 2. |
| 7 | `claude/login-page-and-icon-8b7b57` | `test-login-and-icon.ps1` | Sign out and back in; checks the process name and icon |
| 7b | `claude/in-app-signin` | `test-in-app-signin.ps1` | Sign-in only; the optional last step needs a subscriber-only VOD from a channel you subscribe to. Its handoff explains the web view choice. |

### Tier 2: needs a quick download (a short cropped VOD, about 1–2 minutes)

| # | Branch | Script | Notes |
|---|---|---|---|
| 8 | `claude/ui-download-dialog-themed` | `test-ui-download-dialog.ps1` | All steps but the last are instant; the last is a 1-minute cropped download |
| 9 | `claude/ui-downloads-queue-themed` | `test-ui-downloads-queue.ps1` | Cropped 2-minute VODs; pause/resume per item |
| 10 | `claude/ui-results-toolbar-themed` | (same script) | The badge steps skipped in Tier 1 |
| 11 | `claude/feature-tray-notifications-963477` | `test-tray-notifications.ps1` | The "download finished" notification needs a download to complete |

### Tier 3: needs long downloads or repeated timing runs

| # | Branch | Script | Notes |
|---|---|---|---|
| 12 | `claude/fix-slow-downloads-15ccca` | `test-slow-downloads.ps1 -Rounds 2` | Times master against the branch on 20+ minute sections |
| 13 | `claude/ui-downloads-queue-themed` | (same script) | Its one optional long-VOD step |

### Tier 4: needs waiting for a channel to publish a new video

| # | Branch | Script | Notes |
|---|---|---|---|
| 14 | `claude/feature-auto-download-f77d07` | `test-auto-download.ps1` | The first-run and seen-list checks are quick; only the "new video" check waits |
| 15 | `claude/ui-watched-searches` | `test-ui-watched-searches.ps1` | Instant UI steps and "Check now" come first; the "new video" wait is last and optional. Test after #14. |

The 16 PR branches have their scripts on `test-scripts` (`test-scripts\test-<branch>.ps1`) if a PR needs re-testing after review feedback.

## After testing: suggested merge and PR order (ask the user first)

1. Open the PR for `ui-theme-keys`, then the PRs for dark-inputs, header-nav, search-sidebar and downloads-queue. They all start from `master`, so they can go upstream now; each says "depends on ui-theme-keys".
2. Merge the passing `claude/*` branches into `combined` so the user gets everything day to day. Expected conflicts:
   - **login × tray × header-nav:** `MainWindow.axaml(.cs)`. Login and tray both added a `BringToFront()`; keep one. The tray version also calls `Show()`.
   - **settings-sections × tray × auto-download × theme:** `PreferencesView.axaml` and `Preferences*.cs`. Settings-sections replaces the page layout, so re-add the others as side-menu sections; its handoff says how.
   - **dark-inputs × Coyote:** both add keys to `App.axaml`. Keep every theme defining all 17 keys.
   - **`.claude/handoffs/LATEST.md`:** keep this overview.
3. The `combined`-based UI branches (results, dialog, settings) are rebased onto `master` when #96, #37, #35, #50, #71 and #86 merge upstream.

## Known bugs not yet fixed

- **Pipe name mismatch:** `App.axaml.cs` sends "Activate" on pipe `TwitchLeecher`, but `MainWindow` listens on `TwitchLeecher-DX`. A second launch doesn't bring the running window forward.
- **Startup flash:** on the login branch, a saved sub-only token briefly makes the window Topmost at startup.
- **macOS isn't supported:** no `ffmpeg.exe`, "open folder" doesn't work, and there's no notification backend.

## Suggested next (not started)

- **`deps-patch-updates` from `master`:**
  - Avalonia 11.3.11 → 11.3.23 (every Avalonia package);
  - Avalonia.ReactiveUI 11.3.8 → 11.3.9;
  - CommunityToolkit.Mvvm 8.4.0 → 8.4.2.
- **`deps-remove-unused` from `master`:** remove System.Data.SQLite (unused, ships native DLLs), System.ComponentModel.Annotations (built into .NET) and the Upgrade Assistant analyzers (3 projects).
- **Later:**
  - Avalonia 12, after the UI PRs settle; check that the icon and progress-ring add-ons support it;
  - replacing Ninject (an unmaintained beta);
  - #31, the async rewrite, which also removes the obsolete `WebClient`;
  - #42 (hover preview) and #36 (CLI).
