# Handoff: TwitchLeecher-Dx, prepare the pull requests (master overview)

Updated 2026-11-19 (late night), on the Windows PC. **Every branch below has now been built and tested by the user on Windows.** The next job is to turn the tested branches into clean upstream PRs, one PR per change.

Read this file from `combined` (`git show combined:.claude/handoffs/LATEST.md`). Other branches carry their own older, branch-specific `LATEST.md` or `ui-*.md` notes; this overview is the one to trust.

## Rules (from the user, keep them)

- **Never name, link or credit the third-party Twitch downloader project used for ideas.** Code, comments, commits, PR text, docs.
- **Each change is its own branch and its own upstream PR**, so the developer can pick and choose. Don't bundle.
- **PR branches and PR text must contain no reference to Claude or any AI**: no `Co-Authored-By` line, no "Generated with" line, no `.claude/` files, no test scripts. (Working branches still carry the `Co-Authored-By: Claude Opus 5.5` trailer; the recipe below strips it.)
- **PR "Testing" sections claim only what the user actually confirmed** (see the results below). Do not invent coverage.
- Commit freely on working branches. **Push, force-push and open PRs only when the user asks.** Never commit to `master`.
- Ask before merging branches into each other or opening PRs to upstream.
- Mark known limits in code as `// shortcut: <limit>, <when to upgrade>`.
- **Views use theme brushes only** (`{DynamicResource <Key>Brush}`), never hex colours (translucent black overlays and the red LIVE badge are the allowed exceptions).
- **A theme must define every redesign key twice: as a `Color` AND as a literal `SolidColorBrush`** (see "Lessons"). New themes must do the same.

## Project and PC setup

- C# .NET 10, Avalonia 11.3, Ninject, CommunityToolkit.Mvvm. Solution in `TwitchLeecher/`; app project `TwitchLeecher/TwitchLeecher/TwitchLeecher.csproj`. Fork `bluefroguk1/TwitchLeecher-Dx`; upstream `schneidermanuel/TwitchLeecher-Dx`. Design canvas: https://claude.ai/artifact/CYBdwo46oR2LqydWnUdo62
- **.NET SDK is at `C:\Program Files\dotnet\dotnet.exe`, not on PATH by default** (an x86 `dotnet` without an SDK comes first). Build: `& 'C:\Program Files\dotnet\dotnet.exe' build TwitchLeecher\TwitchLeecher\TwitchLeecher.csproj -c Release` (0 errors is the bar; ~20 warnings are normal).
- Run `bin\Release\net10.0\TwitchLeecher.exe` (not `dotnet TwitchLeecher.dll`, which shows as ".NET Host").
- **The app is single-instance.** A hidden/tray instance, or one left by a test script, blocks new launches and locks `bin\` so builds fail with "file is locked". Kill every `TwitchLeecher` process (`Get-Process TwitchLeecher | Stop-Process -Force`) before building or launching.
- Terminal tool: `run_in_terminal` types literal lines only. Start scripts with `$env:Path = 'C:\Program Files\dotnet;C:\Windows\System32;C:\Windows;C:\Windows\System32\WindowsPowerShell\v1.0;C:\Program Files\Git\cmd'; powershell -ExecutionPolicy Bypass -File test-scripts\<script>.ps1 -SkipBuild`.
- **App settings are shared by every branch** (`%APPDATA%\Twitch Leecher DX\config.xml`). The user's saved Theme is "Original" (the code default is "New"); Quick download and similar toggles persist between branches and change behaviour (e.g. "Disable conversion" hides the crop section). Check them when something looks wrong.
- ffmpeg on `master` is the old committed 5.0. PR #110 replaces it with 9.0.2 fetched at build time (the 9.x exe is over GitHub's 100 MB file limit).

## Where things stand

- **16 PRs are open** upstream (#104–#119), all from `*-pr` branches on the fork, no reviews or comments yet. They are a separate, earlier body of work (bugfixes, HEVC, clips, chat, quick-download, keep-.ts, ffmpeg 9, direct-to-ffmpeg, search-error, page scroll, three perf fixes, #50, #86, #35). `combined` = all of them merged. The `test-scripts` branch README maps each script to its PR. Check `gh pr list --repo schneidermanuel/TwitchLeecher-Dx --author bluefroguk1` for review feedback before anything else.
- **The 13 branches below are new and NOT yet PRs.** All pass their test scripts, with the exceptions noted.
- **Local is the source of truth; the fork's copies are older.** Most branches were rebased or extended locally and have 1–4 unpushed commits; several fork branches hold earlier versions of the same commits (some pushed from GitHub Desktop at ~21:47). Pushing a rebased branch needs `git push --force-with-lease origin <branch>`. Do not push until the user asks.

## Branch status (13 tested)

"Own commits" = what the branch adds beyond its base. Tested = user confirmed on Windows.

### UI redesign, cut from `master` (can become PRs now, in this order)

| # | Branch | Own commits | Tested / fixes made on the PC |
|---|---|---|---|
| 1 | `ui-theme-keys` | f001555 keys, 07ed4ee literal brushes | tested through the branches on it. **Its PR goes first.** |
| 2 | `claude/ui-dark-inputs-0028fd` | 030213a dark inputs, 7bc110c build fix, 923e88c invisible-text/dropdown/radio fix | passed. Fixed: build error (`VerticalContentAlignment` on a grouped selector), invisible input text (brush bug), blank dropdown, radio-label dark patches |
| 3 | `claude/ui-header-nav-d65209` | 58dc6df header+toast, b7dd1fa theme keys, f242706 | passed. **User replaced the Account dropdown with Info / Donate / Log out icon buttons.** Test script and notes updated |
| 4 | `claude/ui-search-sidebar-93487b` | 119d72a, c5a49e9 | passed (opens on Search, Welcome page removed) |
| 5 | `claude/ui-downloads-queue-themed` | 2c41f9e queue, c049059 theme keys, 33a265c | passed (per-item pause/resume, speed/ETA, failure reasons) |

### UI redesign, cut from `combined` (need rebasing onto `master` before a clean PR)

| # | Branch | Own commits (not in `combined`) | Depends on | Tested / fixes |
|---|---|---|---|---|
| 6 | `claude/ui-results-toolbar-themed` | 0fa5a11, f5dbeb3 (+ theme keys, literal brushes) | ui-theme-keys, #96, #37 | passed incl. badges. Test script's build/exe path was broken and is fixed |
| 7 | `claude/ui-download-dialog-themed` | 39f08dc filmstrip dialog, f57deec, 3595229 build fix, 2594d0b | ui-theme-keys, #35, #50, #71, #37 | passed. User asked for and got: overlapping crop handles can be pulled apart by drag direction; From/To boxes accept only digits and `:` (paste still unfiltered, a `// shortcut:`) |
| 8 | `claude/ui-settings-sections-v2` | 2d15ecf, 1039903, 9bdb9b8 | ui-theme-keys, #96, #86 | passed. Fixed: the "unsaved changes" bar showed on open; `HasChanges` now compares against the saved values |
| 9 | `claude/feature-new-theme-645696` ("Coyote") | 12155ab/23caa2e theme, d11f8e7 | ui-theme-keys | passed. Fixed: tab labels were fixed black (now a per-theme brush), progress bar brushes were invisible (now literal brushes) |

### Other features, cut from `combined`

| # | Branch | Own commits | Tested / notes |
|---|---|---|---|
| 10 | `claude/login-page-and-icon-8b7b57` | 6a18e33 | passed (login page, window comes to front, exe icon). The "second launch activates window" check hits the known pipe bug below |
| 11 | `claude/feature-tray-notifications-963477` | 06595a9 tray, 4d038e7 | **tray and "Minimise to tray" pass. Desktop notifications were REMOVED** (see below). The branch name is now misleading; consider `feature-tray` |
| 12 | `claude/fix-slow-downloads-15ccca` | 0ea87ad `Parallel.ForEachAsync` | manual checks pass, but **the speed test did NOT show a gain**: master 8.74 MB/s vs branch 8.31 MB/s (0.95x, within noise; line-limited at ~70 Mbit/s; round 2 sections differed in size). Do **not** claim a speed-up. If opened at all, present it as connection-handling cleanup (uses the full Max connections setting; a cancel no longer retries) |
| 13 | `claude/feature-auto-download-f77d07` | 2b6509f | passed (background channel monitoring) |

### Watched searches (supersedes #13 in the UI)

| Branch | What | Status |
|---|---|---|
| `claude/ui-watched-searches` | auto-download reworked as "watched searches" (per-search quality/When found, Results "Watch this search", Downloads pill). Base = auto-download + downloads-queue merged | **passed**, but the user said its section "isn't in keeping with the theme": the dark cards sit on the old flat grey Settings page |
| `claude/ui-watched-searches-in-settings` (NEW) | the above merged with `ui-settings-sections-v2`; Watched searches is its own Settings side-menu section; the Downloads pill sets `ScrollToWatchedSearches`, which selects that tab. Merge commit f547349 | **builds (0 errors); user has NOT yet confirmed it looks right.** Ask first. If the cards still clash, restyle (flatter rows) |

Also: `claude/ci-build-41fa8a` (GitHub Actions build + Windows zip, passed earlier; exists only on the fork). `master` is a clean copy of upstream: leave it alone.

## Decisions and results to carry into the PRs

- **Notifications removed from the tray branch.** On Windows the toast backend (`DesktopNotifications.Windows`) initialised and `ShowNotification` returned without error, yet no toast ever appeared (Do Not Disturb checked). Removed: the notification service/interface, the "Show notifications" setting, the download hooks, the `DesktopNotifications*` packages and the Windows-specific target framework (back to plain `net10.0`). To revisit: register the app's toast identity (AppUserModelID + Start Menu shortcut) for an unpackaged exe, or ship packaged. The auto-download and watched-searches branches may call `IDesktopNotificationService.Notify` in their handoffs; that interface no longer exists on the tray branch.
- **Slow downloads:** see row 12. Network, not the download loop, is the bottleneck on this line (timing: 1 connection ≈ 36 Mbit/s, 9–12 connections ≈ 53–62 Mbit/s).
- **Header buttons:** Info / Donate / Log out are icon-only so the header still fits at the 780px minimum width.

## Second test round: fixes and additions (2026-11-20), START HERE

Everything below is on the branch it belongs to (for its PR) **and** merged into `combined-all`. **`combined-all` compiles on GitHub Actions** (Windows + Linux, run 44657053198, commit 194a3bc). None of it has been run yet. Get the latest Windows zip from that run's Artifacts, or `git pull` on `combined-all` and build.

Before testing, kill any running copy: `Get-Process TwitchLeecher | Stop-Process -Force`.

### 1. Instant checks (UI only)

| Fix | Branch | What to check |
|---|---|---|
| Search: no ★ by the Channel field | `claude/ui-search-sidebar-93487b` (56835dd) | Only the favourites chips under the field |
| Search: no bottom Search/Cancel | same | The bottom bar is empty on Search; the sidebar button and Enter (in the Channel field) still search |
| Search: Links / IDs box | same | **Root cause:** Enter-to-search was bound to the whole page, so Enter in this box searched instead of adding a line. Now each link goes on its own line, long links wrap, and the box scrolls |
| Header: no Sub-only button | `claude/ui-header-nav-d65209` (596c24d) | Top bar shows tabs, Settings, Info/Donate/Log out only |
| Settings › General: Sub-only row | `claude/ui-settings-sections-v2` (f03c4e2) | SUB-ONLY DOWNLOADS shows On/Off, and "Set up" opens the sub-only page |
| Settings › Search: no favourites list | same | Only a DEFAULT CHANNEL box (Search on start uses it). Favourites are managed with the ★ on results |
| Watched searches row layout | `claude/ui-watched-searches-in-settings` (3397903) | Each row on one line: title, "When found", dropdown, Edit, Active, a red bin. Rows line up, nothing is cut off, even at the minimum window width |
| Watched searches bin | same | The bin asks "Stop watching …?"; Edit no longer has Remove |
| Dialogs centred | `ui-draggable-dialogs` (edf3118) | Move the window to a corner or second monitor, trigger a message box (search a channel that doesn't exist). It opens centred over the window |
| Settings › Download: compression | `feature-compress-video` (3ec4898) | "Compress videos after download" plus a level (Light / Balanced / Strong) under Quick download |

### 2. One short download (3-minute crop)

| Item | Branch | What to check |
|---|---|---|
| Chat as JSON | `feature-chat-html` (1e68853) | Dialog › Extras › Save chat › "as JSON (for other tools)" writes `<video>.chat.json`. The script parses it and checks the messages are in order |
| Compress after download | `feature-compress-video` | Dialog › Extras › "Compress after download" follows Settings. Status goes Downloading → Converting → **Compressing** with progress, and the log says "Compressed X → Y". The file is HEVC (Balanced), plays, and is smaller. Cancelling while compressing leaves no `.compressing` file. **CPU encoding: a long VOD takes a while** (GPU encoders are a noted follow-up) |
| Dependency updates | `deps-patch-updates` (e1868c6), `deps-remove-unused` (7a0d896) | The app starts, signs in (uses the localhost listener), searches, downloads, opens Settings. Avalonia is 11.3.23, Mvvm 8.4.2, ReactiveUI 11.3.9. SQLite, Annotations, the migration analysers, stale DLL hint paths and the unused ASP.NET framework reference are removed, so the zip should be noticeably smaller |

### 3. Needs a channel that is live now

| Fix | Branch | What to check |
|---|---|---|
| **Watched search downloaded a stream still in progress** (kibblethegoat) | `claude/ui-watched-searches-in-settings` (3397903) | Twitch now gives a recording VOD a normal thumbnail, so the old live check missed it. Broadcast searches now ask Helix `/streams` whether the channel is live, and hold back VODs from the current stream (WAITING … still live). If that check fails, the newest VOD is held back. Test: a watched Broadcasts search for a live channel. Check now must show WAITING and start nothing; after the stream ends, the next check starts it |

### Answers to the user's questions this round

- **.NET:** stay on .NET 10 (LTS, supported to Nov 2028). .NET 11 is out but adds nothing needed. The safe package updates and removals are the two `deps-*` branches. Avalonia 12 and replacing Ninject come later.
- **Chat export:** text, HTML with emotes, offline HTML, and now **JSON**.

## Post-testing notes: built and in combined-all (2026-11-20)

All the user's notes and both tidy-ups are built on the branch each belongs to (for its PR) and merged into `combined-all`. **`combined-all` compiles on GitHub Actions** (Windows + Linux, run 44646475701). None of this has been **run** yet. Test on `combined-all`, quickest first; each branch's own test script has the detailed steps.

### Instant (UI only)

| Note | Branch (for the PR) | What to check |
|---|---|---|
| 1 | `claude/ui-search-sidebar-93487b` (9c5668b) | On results, the Search tab opens the search form; "← Back to results (N)" returns to them |
| 3 | `claude/ui-results-toolbar-themed` (a49fd65) | Channel searches show the channel name, its avatar and a ★ that adds or removes the channel from favourites (and saves). Hidden for link/ID searches |
| 2 | `ui-draggable-dialogs` (49f659d, from `master`) | Message boxes can be dragged by their header |
| 7 | `claude/ui-watched-searches-in-settings` (33e58a4) | Settings › Watched searches uses flat rows that match the other sections. **The user decides whether the look is right** |
| tidy | `claude/ui-download-dialog-themed` (39bced0) | No odd font sizes left in the dialog |
| tidy | `combined-all` only (1fd18f0) | The results page uses `IQuickDownloadService` (plus a new `GetDefaultPath`) instead of its own copy. Quick download, Download Selected and the QUEUED/DOWNLOADED badges behave as before |
| 5 | `claude/feature-tray-notifications-963477` (4b0930f) | The first minimise asks "Minimise to the system tray instead?" with "Don't ask again". The answer sets Settings › General › "Minimise to the system tray". To re-test, set `AppTrayPromptShown` false in `config.xml` |
| pipe | `fix-single-instance-pipe` (9a1ea44, from `master`) | With the app running (or hidden in the tray), launching it again brings the existing window forward |

### Needs one short download

| Note | Branch | What to check |
|---|---|---|
| 4 | `feature-chat-html` (490a50e, built on `feature-71-chat-pr` / PR #107) | Download dialog › Extras › "Save chat" offers **as text / as HTML with emotes / as HTML (works offline)**. Open the `.chat.html` in a browser and check name colours, emotes and timestamps. For offline mode, check it with the network off. Third-party emotes (7TV/BTTV/FFZ) are not included |
| 8 | `feature-desktop-notifications` (3d1ca5a) | Desktop notifications are back. On first run the app creates a Start Menu shortcut with AppUserModelID `TwitchLeecher.Dx` (Windows needs it to show toasts from an unpackaged exe). Check that a toast appears when a download finishes, and "All N downloads finished" after a batch. If toasts still don't show, its handoff lists what to check |

### Needs a sign-out/sign-in of Windows, or a reboot

| Note | Branch | What to check |
|---|---|---|
| 6 | `feature-start-with-windows` (265c397, includes the pipe fix) | Settings › General › "Start Twitch Leecher when I sign in", and "Start minimised to the tray". After signing in to Windows, the app starts, hidden in the tray if that's ticked. Turning it off removes `HKCU\...\Run\TwitchLeecher` |

### Needs a channel that is live

| Fix | Branch | What to check |
|---|---|---|
| live at setup | `claude/ui-watched-searches-in-settings` (32182c7) | Add a watched search for a channel that is **live right now**. The first check's log says "the current live stream will download when it ends"; after the stream ends, that VOD downloads (or waits paused) |

**Merge notes for `combined-all`:**
- The tray, start-with-Windows and notification toggles all sit in the redesigned Settings › General.
- The chat-format choice sits under the new dialog's Extras.
- The results page keeps the channel-header lookup through `IApiService`.

**Still not in `combined-all`:**
- `claude/fix-slow-downloads-15ccca`: it conflicts with the queue's pause/resume, and showed no measurable gain.
- `claude/in-app-signin`: never tested.

## combined-all: everything together, for one last round of testing (built 2026-11-20 on the Mac)

`combined-all` = `combined` (the 16 open PRs) plus every tested branch. **It is a test branch only**: PRs are still cut per branch with the recipe below. It has **not been compiled**, because the Mac has no SDK. Build it first and fix any compile errors on `combined-all` itself, noting which branch each fix belongs to.

**Merged in this order:** ui-theme-keys, dark-inputs, header-nav, search-sidebar, watched-searches-in-settings (which brings auto-download, the Downloads queue and settings-sections-v2), downloads-queue-themed (recorded only: it differs just by the brush fix, which ui-theme-keys already brought), results-toolbar, download-dialog, Coyote, login-page-and-icon, tray.

**Left out:**
- `claude/fix-slow-downloads-15ccca`. It rewrites the same chunk loop the Downloads queue now uses for per-item pause and resume (`loopState`, kept chunks). The speed test showed no gain. Merging it would mean redesigning pause around `Parallel.ForEachAsync`. Decide whether to keep it as a PR at all.
- `claude/in-app-signin`. Never tested; it conflicts with login-page-and-icon in `AuthListener.cs`.

**How the conflicts were resolved.** Check these first; each is a place where two branches met:
1. `MainWindow.axaml`: the new header wins, with the window `Icon` attribute from `combined` kept.
2. `SearchView.axaml`: the sidebar layout wins, with a **"Clips"** type chip re-added (from the clips PR #37).
3. `MainWindowVM.ShowSearchOnStart()`: it now starts the watched-searches loop, opens Search, then runs the start-up search. The Welcome page is gone.
4. `PreferencesView.axaml`: the side-menu Settings page wins (with Watched searches as a section). The tray's **"Minimise to the system tray"** toggle was added under General.
5. `SearchResultViewModel.cs`: the results-toolbar version wins, with **"Watch this search"** (the command, the `IAutoDownloadService` injection and the bottom-bar button) added back. Note: the results VM keeps its own copy of `CreateDownloadParams`/`TryEnqueue`, while auto-download moved the same logic into `IQuickDownloadService`. It works, but it's duplicated. Before the PRs, make results use `IQuickDownloadService` plus its `GetDefaultTarget` helper.
6. `DownloadView.axaml`: the redesigned dialog wins. Two small leftover font sizes (11 and an odd size) should become 12.
7. `App.axaml`: Coyote's dictionary added. All three themes define the same redesign keys, with no duplicates and nothing shared. Coyote alone overrides the SimpleTheme control brushes, on purpose.
8. `MainWindow.axaml.cs`: one `BringToFront()`, public, posting to the UI thread and calling `Show()` (for the tray). It runs on the pipe "Activate", on the tray click, and after sign-in.

**What to test on combined-all (cross-branch, quickest first):**
- Switch theme New → Original → Coyote on every page: header, sidebar, results, dialog, queue, Settings (all sections), the watched searches section. Look for transparent or unreadable text.
- Search → results → Search tab → Downloads tab → back. Check the active-tab underline, the counts, and the results scroll position.
- The Clips chip: a clips search, clip badges, quick download of a clip, and the dialog hiding the crop and chat options for clips.
- "Watch this search" from results. Then the Downloads pill takes you to Settings › Watched searches.
- Minimise to tray from the new General toggle; the tray restores the window. Sign out and in again: the window comes to the front.
- One short cropped download with chat + VOD info: queue speed and ETA, pause and resume, results badges (QUEUED, then DOWNLOADED).

## Step 1 (done 2026-11-20) for the PC: push every tested branch (the Mac cannot see the fixes)

Most fixes made during testing exist only on the PC. For example, 07ed4ee, 923e88c, f242706, 33a265c, 2594d0b, d11f8e7, 4d038e7 and f547349 are **not on GitHub**. The Mac pushed its own brush-fix commits (29bb1b3 and siblings, about 21:47) to the same branches; the PC versions replace them. Push them all from the PC:

```powershell
git fetch origin
$branches = 'ui-theme-keys','claude/ui-dark-inputs-0028fd','claude/ui-header-nav-d65209','claude/ui-search-sidebar-93487b','claude/ui-downloads-queue-themed','claude/ui-results-toolbar-themed','claude/ui-download-dialog-themed','claude/ui-settings-sections-v2','claude/feature-new-theme-645696','claude/login-page-and-icon-8b7b57','claude/feature-tray-notifications-963477','claude/fix-slow-downloads-15ccca','claude/feature-auto-download-f77d07','claude/ui-watched-searches','claude/ui-watched-searches-in-settings'
foreach ($b in $branches) { git push --force-with-lease origin "${b}:${b}" }
```

Before pushing, make sure the PC copy of each branch contains everything from the Mac's version. The only Mac-only commits are the literal-brush fixes, which the PC already has as its own commits. If unsure, run `git log origin/<b> --not <b> --oneline` per branch first and check every listed commit is a brush fix.

## combined-all plan (built: see the section above)

`combined` (the 16 open PRs) plus every tested branch, merged in this order:
1. ui-theme-keys
2. dark-inputs
3. header-nav
4. search-sidebar
5. downloads-queue
6. results-toolbar
7. download-dialog
8. settings-sections-v2
9. Coyote
10. login-page-and-icon
11. tray
12. slow-downloads
13. watched-searches-in-settings, which brings in auto-download

`claude/in-app-signin` was **not tested** and conflicts with login-page-and-icon, so it is left out of the first build; it can be added as a last commit. The PC builds and tests `combined-all` to look for cross-branch bugs before the PRs are cut. `combined-all` is a test branch only; PRs are still cut per branch.

## User's notes from testing: original implementation plan (now built, see "Post-testing notes" above)

Each is its own branch. Build it on `combined-all` so it can be tested with everything else, then extract it with the PR recipe.

1. **Search tab goes to the search form.** `MainWindowVM.ShowSearch()` sends the user to the results page whenever results exist. Change it so the Search tab always opens the form (`_navigationService.ShowSearch()`). The form then shows a "Back to results (N)" link when results exist, and the tab badge keeps showing N. Small: about 10 lines in `MainWindowVM` and `SearchView`.
2. **Dialogs can be dragged.** `MessageBoxWindow` (and any other custom-framed window) has no title bar to grab. Add one small reusable behaviour: on `PointerPressed` with the left button over the dialog's header, call `window.BeginMoveDrag(e)`. Attach it to every dialog header. Small, one helper.
3. **Channel header with ★ favourite on results.**
   - For channel searches, the results summary bar shows the channel's display name (and its avatar from Helix `users.profile_image_url`), plus a ★ toggle.
   - The toggle adds or removes the channel in `Preferences.SearchFavouriteChannels` through `IPreferencesService` and saves.
   - It sits next to "Watch this search". Hide it for Links/IDs searches. Small to medium; it touches the results-toolbar view.
4. **Chat export with emotes.** Settings › Download gets "Chat format: Text / HTML". In `GetChatMessages`, keep each fragment's `emote.emoteID` and the commenter's `userColor`, which come in the same GQL response.
   - The HTML writer outputs coloured names, timestamps and `<img>` emotes from `https://static-cdn.jtvnw.net/emoticons/v2/{id}/default/dark/1.0`.
   - Offer "Embed images (works offline)", which downloads each distinct emote once and inlines it as base64.
   - 7TV, BTTV and FFZ emotes would be a later add-on. Medium.
5. **First-minimise tray prompt** (on the tray branch).
   - On the first change to `WindowState.Minimized` while `!Preferences.TrayPromptShown`, show "Minimise to the system tray instead?" with Yes/No and "Don't ask again".
   - Yes sets `AppMinimiseToTray`. Either answer with the box ticked sets `TrayPromptShown`, then saves and applies. Settings shows the same toggle. Small.
6. **Start with Windows, minimised to the tray.**
   - Settings › General: "Start Twitch Leecher when I sign in to Windows" and "Start minimised to the tray".
   - Windows: `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\TwitchLeecher` = `"<exe path>" --minimized`, written or removed when the toggle changes.
   - Linux: `~/.config/autostart/twitchleecher.desktop`.
   - `App.OnFrameworkInitializationCompleted` reads `--minimized`, keeps `MainWindow` hidden and shows only the tray.
   - **Fix the pipe-name bug first** (Known bugs). Without it, a second launch can't wake the hidden instance. Small to medium; needs the tray branch.
7. **Watched searches look** (the user said it "isn't in keeping with the theme"). On `ui-watched-searches-in-settings`, restyle the list as flat rows like the other Settings sections: no nested dark cards. Use `CardBackgroundBrush` only for the row hover and selection, and match the spacing and headings of `SettingsAfter`. Get the user's OK on the look before cutting the PR.
8. **Desktop notifications, revisited.** Toasts from an unpackaged exe only show once the app has a registered identity. Create a Start Menu shortcut carrying an AppUserModelID (the notification library can do this on first run) and set the same ID on the process. Retry on its own branch.

## Known bugs, not fixed

- **Pipe name mismatch:** `App.axaml.cs` sends "Activate" on pipe `TwitchLeecher`, but `MainWindow` listens on `TwitchLeecher-DX`, so a second launch does not bring the running window forward. One-line fix; do it as its own branch from `master`.
- **Startup flash:** on the login branch, a saved sub-only token briefly makes the window Topmost at startup.
- **macOS isn't supported** (no `ffmpeg.exe`, "open folder", no notification backend).
- Pasted text bypasses the From/To digit filter (checked on blur, as before).

## Lessons (read before touching colours or XAML)

- **A `SolidColorBrush` whose `Color` is a `{DynamicResource ...Color}` does not resolve after `App.SetTheme()` clears and refills `Application.Resources`.** Anything using it renders transparent (this hid all input text, then the progress bar). Define brushes with literal colours inside each theme dictionary (`New`, `Original`, `Coyote`) and remove indirect ones from `Shared`. Old brushes still defined indirectly (`MainWindowBackground`, `TlScrollingTextBox*`, ...) work only because they are read with `StaticResource` at startup; don't use `DynamicResource` on them. Duplicate keys between `Shared` and a theme dictionary make the theme switch throw.
- **Avalonia XAML:** a grouped selector (`TextBox, ComboBox, NumericUpDown, CalendarDatePicker`) fails to compile if a setter such as `VerticalContentAlignment` isn't on the common base. Split per control. Template part names (`Border#border`, `ContentPresenter#PART_ContentPresenter`, `Ellipse#border`) come from SimpleTheme.
- **Windows tooling:** files are CRLF; Python edits must normalise `\r\n` first and assert the match. In Python heredocs, `\n` inside a path string becomes a newline (this corrupted `bin\Release\net10.0`); use real backslashes via byte constants or `\x5c`. Put multi-line PR bodies in a script file, not nested shell quoting.
- **Never `git add -A` from a subfolder:** it commits `.claude/handoff-nudge-state.json`. Add files by path.
- The test scripts differ: most take `-SkipBuild`; `test-ui-results-toolbar.ps1` always builds; the older scripts launch `dotnet TwitchLeecher.dll`.

## How to make a clean PR branch (the recipe that worked for #104–#119)

For each change, from the tested branch:

```bash
git checkout -B <name>-pr <tested-branch-or-commit-range-tip>
FILTER_BRANCH_SQUELCH_WARNING=1 git filter-branch -f --prune-empty \
  --index-filter "git rm -r -q --cached --ignore-unmatch test-scripts .claude TwitchLeecher/.claude" \
  --msg-filter "sed -e '/Co-Authored-By: Claude/d' | sed -e :a -e '/^\n*\$/{\$d;N;ba' -e '}'" master..HEAD
git reset --soft master && git commit -m "<clear message>"      # one commit per PR
git diff master..HEAD --stat; git diff master..HEAD | grep -ci claude   # must print 0
git push -u origin <name>-pr
```

Then `gh pr create --repo schneidermanuel/TwitchLeecher-Dx --base master --head bluefroguk1:<name>-pr`. Body: Summary, Testing (only what was confirmed), a note naming any PR it overlaps. Write the body from a Python/script file. Verify the diff is only that change.

## Suggested order for the PRs (ask the user first)

1. **`ui-theme-keys`**, then the four master-based UI branches (`dark-inputs`, `header-nav`, `search-sidebar`, `downloads-queue-themed`). Each says "depends on ui-theme-keys"; either stack the PR bases or open them after it merges.
2. **The `combined`-based branches** (results-toolbar, download-dialog, settings-sections-v2, Coyote, login, tray, auto-download/watched-searches, slow-downloads): extract each branch's own commits (`git log combined..<branch>`), drop the cherry-picked `ui-theme-keys` commits, and `git rebase --onto master <combined-or-base> <branch>`. Where the dependency PRs (#96 = #108, #37 = #106, #35 = #119, #50 = #117, #71 = #107, #86 = #118) are still unmerged, either wait for them to merge or state the dependency in the PR text. Login, tray and Coyote touch little and may cherry-pick straight onto `master`.
3. **Watched searches:** open as one PR only after auto-download and the settings-sections PR exist; use `ui-watched-searches-in-settings` as the source if the user approves its look.
4. `slow-downloads`: only with honest wording (row 12), or drop it.
5. The pipe-name fix as its own tiny branch from `master`.

## Expected merge conflicts when combining branches

- `MainWindow.axaml(.cs)`: login × tray × header-nav (both login and tray add `BringToFront()`; keep one, the tray version also calls `Show()`).
- `PreferencesView.axaml` and `Preferences*.cs`: settings-sections × tray × auto-download × theme. Settings-sections replaces the page layout, so re-add the others as side-menu sections (watched searches is already done on `ui-watched-searches-in-settings`).
- `App.axaml`: dark-inputs × Coyote (both add keys). Keep every theme defining all 17 keys as Colors and Brushes, plus `TlTabItemTextForeground` and `TlProgressBar*` brushes per theme.
- `.claude/handoffs/LATEST.md`: keep this overview.
- The 16 open PRs overlap each other and these branches (e.g. #106/#107/#108, #104/#111); see each PR's description.

## Later (not started)

- `deps-patch-updates` from `master`: Avalonia 11.3.11 → 11.3.23 (all Avalonia packages), Avalonia.ReactiveUI 11.3.8 → 11.3.9, CommunityToolkit.Mvvm 8.4.0 → 8.4.2.
- `deps-remove-unused` from `master`: System.Data.SQLite (unused, ships native DLLs), System.ComponentModel.Annotations, the Upgrade Assistant analyzers (3 projects).
- Avalonia 12 after the UI PRs settle; replacing Ninject (unmaintained beta); #31 async rewrite (removes obsolete `WebClient`); #42 hover preview; #36 CLI; macOS support; toast notifications (see above).
