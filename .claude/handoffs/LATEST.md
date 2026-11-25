# Handoff: TwitchLeecher-Dx (master overview)

Updated **2026-11-25 (evening) on the Mac: five features merged into `final`; next PC run is the full check on merged `final`**. Read this file from **`final`**: `git show origin/final:.claude/handoffs/LATEST.md`. Everything above "HISTORY" is current. Everything below it is the record of earlier rounds and loses wherever it disagrees with the top.

## PARKED IDEAS FOR THE MAC (added 2026-11-25; don't act on them until the user says so, e.g. after usage limits reset)

1. **Translations.** How easy is it to add translations or translation files? Look at how UI text is held today (hard-coded in `.axaml` and view models, or in resources), what Avalonia offers (`.resx` + `x:Static`, or a resource dictionary per language), how big the job is, and whether it would suit a PR to the developer. Report only; no code.
2. **The broken "Check for updates" in released versions (3.9.1 and earlier).** They ask GitHub's API for the releases of `schneidermanuel/TwitchLeecher.Old-dx`, a repository that doesn't exist (fixed for the future in #128, but old copies keep asking there). Question: can the developer make that address work, so existing users hear about 4.0? For example by creating a repository with that name under his account and publishing a release in it that points to the real download, or by renaming a repository so GitHub redirects. Check exactly which URL and fields the old `UpdateService` reads (tag name format, version compare, which link it opens), whether GitHub's API follows repository-rename redirects for that call, and what he would need to do. If it works, draft a short suggestion for him (the user decides whether to send it).

## PC RESULTS, merged `final` 532662e (DONE 2026-11-25, evening, on the PC): ALL PASS

Rebuilt (0 errors) and run on the PC after `git pull`. My own local merges were redundant with the Mac's (never pushed; kept as local branches `backup/pc-*`).

| # | Script | Result |
|---|---|---|
| 1 | `test-video-metadata.ps1` | PASS 10/10 |
| 2 | `test-tl-info-chat.ps1` | PASS 22/22 |
| 3 | `test-chat-real-time.ps1` | PASS 6/6 |
| 4 | `test-third-party-emotes.ps1 -Channel xqc` | PASS 6/6 |
| 5 | `test-chat-extras.ps1 -Channel kaicenat` | PASS 9/9 (no cheers in that sample) |
| 5 | `test-chat-extras.ps1 -Channel jynxzi` | **PASS 10/10, 17 cheermotes shown with their amount** |
| 6 | `test-speed-limit.ps1` | PASS 3/3: **1.75 MB/s limited to 2** (was 1.6 before the review fix), 6.12 MB/s unlimited |
| 7 | `test-free-space.ps1` | PASS 5/5 |

**Script fix (on `final`):** the "high-res on: Twitch emotes at 3.0" check compared counts exactly; the chat of a still-recording VOD changes by a few messages between downloads, so it now allows a 5% difference either way and still requires no emote left at 2.0. Not run: the non-Latin display-name channel check (no such channel known) and the by-hand items below.

**By hand, still not done:** the four new Settings rows in all three themes; a `.chat.html` in a browser (emotes, animation, badges, overlays, cheers).

## DONE (results above): PC RUN on merged `final` (2026-11-25, evening)

**What changed on the Mac:** all five feature branches passed, so they are **merged into `final`** (efcba94, CI green on Windows and Linux) together with your three script fixes. The feature branches are deleted (archived on the fork). The upgrade branch is rebased onto it (a1dd0f2). This run checks that the merges work together.

**Your second merge (eea9309):** you merged the five branches into `final` again on the PC, then merged that with the Mac's. That is fine: the code came out the same apart from four dropped blank lines, now put back. Next time, `git pull` first: they were already in `final`.

**Code review fixes since (ab9b5a5), so this run checks them too:**
- **Speed limit:** each chunk waited its full share *after* it had already downloaded, so a 2 MB/s limit gave about 1.6 MB/s. The wait now counts from when the chunk started. In `test-speed-limit.ps1`, the limited rate should be **closer to 2 than last time's 1.6** (start-up and conversion are inside the timing, so a little under 2 is normal). Report the number.
- **Chat extras:** the channel's BTTV/FFZ/7TV emotes, badges and cheers were looked up by the streamer's display name, which fails when it isn't their login name (for example Japanese or Korean names). They now come from the VOD's owner. Worth one extra run of `test-third-party-emotes.ps1` on a channel with a non-Latin display name if you know one; channel emotes should show up.

**Setup** (first fix PATH, see the gotcha below):
```
$env:Path = "C:\Program Files\dotnet;" + $env:Path
git fetch origin
git checkout final
git pull
Get-Process TwitchLeecher -ErrorAction SilentlyContinue | Stop-Process -Force
dotnet build TwitchLeecher\TwitchLeecher.sln -c Release
```
**Close the app** before scripts 3, 5 and 6 (they switch settings in `config.xml` and put them back).

| # | Script | Expect |
|---|---|---|
| 1 | `test-video-metadata.ps1` | 10/10 |
| 2 | `test-tl-info-chat.ps1` | 22/22 |
| 3 | `test-chat-real-time.ps1` | 6/6 |
| 4 | `test-third-party-emotes.ps1 -Channel <big channel>` | 6/6 |
| 5 | `test-chat-extras.ps1 -Channel <channel whose latest VOD is finished, ideally with cheers>` | 10/10, cheermotes checked this time |
| 6 | `test-speed-limit.ps1` | 3/3 |
| 7 | `test-free-space.ps1` | 5/5 |

Run each with `powershell -ExecutionPolicy Bypass -File test-scripts\<script>`. For #5, a channel that was **not live** when you start avoids the "still recording" drift; a channel with lots of bits (cheers) covers cheermotes. If the sampled chat has no cheers, say so and try one more channel.

**By hand (still not done):**
- Settings › Download: the four new rows ("Chat as text: show the time each message was sent", "Saved chat: include badges", "Saved chat: high-resolution emotes", "Max download speed (MB/s)") read well in **all three themes** (Original, New, Coyote) and line up with the rows around them.
- Open a `.chat.html` from #4 or #5 in a browser: Twitch and third-party emotes show, animated ones move, badges sit before names, overlay emotes sit on top of the emote before them, cheers show their amount.

Report one table under this heading, plus the by-hand notes. Fix script problems in the script on `final`, commit by name, push, and say so. Don't change app code: report app failures with the log.

## PC RESULTS (DONE 2026-11-25, on the PC): chapters check and all five feature-branch checks PASS

Run in the order of the sections below. Build: .NET SDK 10.0.404 (x64). **PowerShell gotcha:** the 32-bit `dotnet` (`C:\Program Files (x86)\dotnet`) is first on PATH and has no SDKs, so `dotnet build` says "No .NET SDKs were found". Run `$env:Path = "C:\Program Files\dotnet;" + $env:Path` first. A first run of the chapters check against that stale build gave 6 of 7 failed; after a real build it passed.

| Branch / script | Result | Notes |
|---|---|---|
| `final`: `test-video-metadata.ps1` | PASS 10/10 | VOD 2928816203, chapters Special Events -> QSMP at 60 s |
| `feature/tl-info-chat`: `test-tl-info-chat.ps1` | PASS 22/22 | Script fix: the recorded-date check passed a DateTime where a bool was needed (b9881b7) |
| `feature/chat-real-time`: `test-chat-real-time.ps1` | PASS 6/6 | Settings file put back |
| `feature/third-party-emotes`: `test-third-party-emotes.ps1 -Channel xqc` | PASS 6/6 | Script fix: the "image per emote word" bound now allows 2% more than the text count (5518 vs 5482 on the first run) |
| `feature/third-party-emotes`: `test-chat-extras.ps1 -Channel xqc` | 9/10, the 10th explained | "High-res Twitch emotes at 3.0" was 843 vs 838 at 2.0: the sampled VOD was **still being recorded**, so each download had more chat (7068, 7079, 7107 messages). Same 94 emotes, none at the wrong size. Script fix: allow up to 5% more. **Not rerun in full after the fix.** No cheers in the sample, so cheermotes were **not checked**. 62 overlay emotes were checked and fine |
| `feature/speed-limit`: `test-speed-limit.ps1` | PASS 3/3 | 1.6 MB/s limited to 2, 6.39 MB/s unlimited |
| `feature/free-space-check`: `test-free-space.ps1` | PASS 5/5 | Script fixes: `--quality source` does not exist on every VOD, so it now uses the top listed quality; and the exit code is read reliably (`$p.Handle`). Stopped itself with "Free space on C:\: about 18.97 GB needed, 15.36 GB free" and wrote 0 MB |

The three script-fix commits are **pushed**: tl-info-chat b9881b7, third-party-emotes a8bd83b, free-space-check ba6b93d. No app code changed. The PC is back on `final`, rebuilt.

**Still open:** rerun `test-chat-extras.ps1` on a channel whose latest VOD is finished (and ideally has cheers) to confirm the high-res check and cover cheermotes. By hand, not done: branch 2's and 4's new Settings rows in all three themes, and branch 3's `.chat.html` in a browser (do the emotes show, do animated ones move). Note: tests run headless through `tl.exe`, so no app window opens.

## DONE: PC RUN PART 2 (2026-11-25): five feature branches, each with its own automated check

Five more ideas, each on **its own branch from `final`** (not in `final` until they pass). Every one has a no-click check script that uses the shared `test-scripts\tl-test-lib.ps1`. Do **part 1 below first** (the chapters check on `final`), then these. For each branch, in this order:
```
git fetch origin
git checkout <branch>
git pull
Get-Process TwitchLeecher -ErrorAction SilentlyContinue | Stop-Process -Force
dotnet build TwitchLeecher\TwitchLeecher.sln -c Release
powershell -ExecutionPolicy Bypass -File test-scripts\<script>
```

| # | Branch | What it adds | Script | Notes |
|---|---|---|---|---|
| 1 | `feature/tl-info-chat` | `tl info <vod>` (JSON: details, qualities, chapters) and `tl download --chat text\|html\|html-emotes\|json` | `test-tl-info-chat.ps1` | 2 and 3 are built on this branch |
| 2 | `feature/chat-real-time` | Settings › Download: "Chat as text: show the time each message was sent" | `test-chat-real-time.ps1` | **Close the app first**: it switches the setting in `config.xml` and always puts it back |
| 3 | `feature/third-party-emotes` | BTTV, FFZ and 7TV emotes in HTML chat (linked, or embedded with html-emotes), **plus** chat badges, overlay (zero-width) emotes drawn on the emote before, cheermotes with their amount, animated FFZ emotes, and two settings in Settings › Download: "Saved chat: include badges" (on) and "Saved chat: high-resolution emotes" (off) | `test-third-party-emotes.ps1 -Channel <a big channel you know>`, then `test-chat-extras.ps1 -Channel <same>` | Twitch's own channel may have no BTTV/FFZ/7TV emotes; the scripts then say they can't run. `test-chat-extras` needs the app **closed** (it switches the two settings in `config.xml` and puts them back). Overlays and cheermotes are only checked if the sampled chat has some; it notes when not |
| 4 | `feature/speed-limit` | Settings › Download: "Max download speed (MB/s)", for all downloads together, 0 = no limit | `test-speed-limit.ps1` | Close the app first (same `config.xml` handling). About 200 MB |
| 5 | `feature/free-space-check` | A download stops early with "Not enough free space on X:" when a drive can't hold it | `test-free-space.ps1` (or `-OutDir <folder on a nearly full USB stick>`) | The "too big" part only runs if a drive is small enough; otherwise it is skipped, not failed. A 60 s watchdog kills it if the check didn't stop it |

Exit code 0 means all passed, 1 means a check failed, 2 means it couldn't run (the message says why). **All five scripts were written on the Mac and have never run.** Fix script problems in the script (on its branch), commit, and say so. Don't change app code during the run: report app failures with the table and the download log (`tl` prints it on failure). Report one table per branch under this heading. Afterwards: `git checkout final` and rebuild.

Also worth a look by hand while on each branch: branch 2's and 4's new Settings rows (readable in all three themes), and branch 3's `.chat.html` in a browser (the emotes show, and animated ones move).

## DONE: PC RUN PART 1 (2026-11-25): video details and game chapters on `final`, automated

Downloaded VODs now carry their title, channel, date and VOD link, plus **a chapter per game the streamer played**. VLC, Plex and Jellyfin show these. Chapters are cut to a cropped download. Clips and "keep the original stream" downloads skip it, because they don't go through ffmpeg.

**Automated check, no clicking (about a minute):** on `final` (`git checkout final; git pull`, then a Release build), with the app signed in once:
```
powershell -ExecutionPolicy Bypass -File test-scripts\test-video-metadata.ps1
```
- It finds a public VOD with two game chapters (Twitch's own channel by default) and downloads two audio-only crops with `tl`: one across a game change, one inside a single game.
- It reads the files back with the app's ffmpeg and checks the title, channel, date, link, chapter names and the change at about 60 s.
- It prints a PASS/FAIL table. Exit code 0 means all passed, 1 means a check failed, 2 means it couldn't run (the message says why).
- **The script was written on the Mac and has never run** (no PowerShell there). If it fails for a script reason, fix the script, commit it, and say so in the report.

Report the table under this heading. Optional by hand: open a downloaded VOD in VLC (Playback › Chapter) and check the chapters and title.

**Mac notes on the upgrade-branch run below (2026-11-25):** the R14 fix (ab41308) is reviewed and kept on the upgrade branch. The branch is rebased onto the newest `final`, so it now includes the chapters feature too. The unused DataGrid package is noted in the PR guide as a clean-up for later.

## PC RUN of the .NET 11 + Avalonia 12.2.0 branch (DONE 2026-11-23, late, on the PC): one regression found

Run on `upgrade/net11-avalonia12.2` at 7ce4d3f (= `final` b4226ab plus one commit; the hash in the instructions below, 2fb84f7, is an earlier version of it). The .NET 11 SDK (11.0.100) was installed on the PC with winget; the build had 0 errors and 18 warnings, version 4.0.0, Avalonia 12.2.0. `Backup`/`Restore` worked and the sign-in survived. Afterwards the PC went back to `final` and rebuilt it (the `net11.0` output folders were deleted so the helper cannot pick them up).

**Result: everything passed except R14, and R14 is now FIXED on the branch (2026-11-24, PC): `upgrade/net11-avalonia12.2` is at ab41308 (one commit on top of 7ce4d3f), pushed to the fork.**

**The R14 cause and fix.** On Avalonia 12.2 / .NET 11 the main window's `Loaded` event runs while the window is still hidden (a `--minimized` start). `MainWindowVM.Loaded()` then runs the start-up token check, which publishes the sign-in event (`SubOnlyAuthChangedEvent`), and `MainWindow.OnSignedIn` called `BringToFront()`, which shows the window. (On `final`, `Loaded` only fires once the window is really shown, so this never happened.) Found with a stack trace of who called `BringToFront`. The fix is one condition in `MainWindow.OnSignedIn`: `if (signedIn && IsVisible)`, so a sign-in event no longer pops up a window that is hidden in the tray. It does not change `final`.

**Verified after the fix (PC):** a `--minimized` start stays hidden (no window handle after 35 s, process responsive); the What's new card (with an older saved version) does **not** appear while hidden and appears once the window is opened (by launching the app a second time, which wakes the running window); opening the window from the tray icon also works; the sign-in still worked afterwards (`tl search` returns videos). The rest of R1-R16 was not repeated, because the change only touches that handler.

**Tip for the tray test.** The overflow panel changes order between runs: **hover the first-slot icon and check its tooltip** before clicking. In this session an earlier click landed on another program's icon (SMTC Bridge), which looked like a failure. Also, if a process seems to vanish, check `Get-Process TwitchLeecher` before assuming a crash (no crash event was recorded).

**Still to decide (Mac):** whether to merge the upgrade branch into the plan as the optional step after the other PRs, as in `.claude/pr-drafts/README.md`.

| Step | Result | Evidence |
|---|---|---|
| R1 | PASS | Settings > General: "Signed in as ...", no sign-in prompt |
| R2 | PASS | Broadcasts (8), clips (7), pasted link: all returned cards with thumbnails |
| R3 | PASS | 1-minute crops saved with chat as HTML plus details, as JSON and as text: all three had the same 2 messages with timestamps; the `.info.json` has its fields. The video was a **1440p60 HEVC** `.mp4` (2560x1440, hevc + aac, exactly 60.0 s), so HEVC works on this build |
| R4 | PASS | A 7-second clip: h264 + aac, 7.55 s |
| R5 | PASS | An uncropped 1440p VOD was started; **Cancel** ended as "Canceled" |
| R6 | PASS | The "keep unconverted video" setting was ticked, unticked and saved, and stayed unticked after a restart |
| R7 | PASS | `tl search <channel> --last 3` listed 3 videos, exit 0 |
| R8 | PASS | `tl --bogus` exit code 2 |
| R9 | PASS | Dark: "What's new in 4.0", 8 items, purple icons, centred on the main window |
| R10 | PASS | **Full release notes** opened the GitHub "Page not found" page (expected 404); Esc closed the card |
| R11 | PASS | After a restart no card |
| R12 | PASS | Classic: gold icons, readable. Enter closed it only after a click on the card (same as on `final`) |
| R13 | PASS | Coyote: blue icons, red **Got it**, readable |
| R14 | **FAIL** | Started with `--minimized`: the main window **and the card appeared on their own**. On `final` the same start stays hidden in the tray until opened from the tray icon. The process command line was confirmed to contain `--minimized`. The Windows startup entry passes `--minimized`, so on this build "Start Twitch Leecher when Windows starts" would open the window instead of the tray |
| R15 | PASS | Fresh settings folder: "Connect with Twitch", no card. **The sign-in web view works:** pressing Connect showed Twitch's "Log in to Twitch" form (nothing was typed) |
| R16 | PASS | `Restore` put theme Original and version 4.0 back |

Other things looked at:
- **Styled controls:** the Custom date pop-up calendar (rounded card, gold selected day, dimmed future days), the **Hide downloaded** switch (gold on, grey off) and the sort drop-down all work. Two small differences to compare with `final` if you care: a thin **blue focus ring** around the selected day in the calendar, and the **highlighted item in the sort drop-down is blue**.
- **Log page and Log > Copy:** the log page shows the full text, and **Copy** put the whole log (3,723 characters) on the clipboard. **There is no DataGrid anywhere in the app now** (a code search finds none), so the "DataGrid on the Log page" line in the instructions below no longer applies.
- **Tray:** opening the window from the tray icon works. The `--minimized` problem is in R14.
- **Update check:** at start-up the only error log was the known "Failed to create ShellLink object" one. (Restore replaced the logs folder, so only the last session's log could be read at the end.)
- **Likely cause of R14 (not investigated further):** `App.axaml.cs` keeps the window hidden on `--minimized` by assigning `desktop.MainWindow = mainWindow` **after** the lifetime has started (`Dispatcher.UIThread.Post`). On Avalonia 12.2.0 / .NET 11 that assignment seems to show the window. A fix would hide it explicitly instead (for example create the window but do not assign it until the user opens it from the tray, or hide it right after the assignment), then repeat R14 and R9-R13 on the tray path.
- **Not done:** R17 (logout) was not repeated, and Audio Only chat sampling was not repeated.

### Original instructions for this run

A separate branch, **not** `final`: `upgrade/net11-avalonia12.2` (2fb84f7, CI green on Windows and Linux). It needs the **.NET 11 SDK** on the PC (`dotnet --list-sdks` should show 11.0.x; if not, ask the user to install it from Microsoft's .NET download page).
```
git fetch origin
git checkout upgrade/net11-avalonia12.2
Get-Process TwitchLeecher -ErrorAction SilentlyContinue | Stop-Process -Force
dotnet build TwitchLeecher\TwitchLeecher.sln -c Release
```
The build output is now `bin\Release\net11.0`: the helper script finds it by itself. Check the title still says 4.0, then do the **whole automated run in 1b (R1–R16)**, plus chat as JSON and text, and the update check. This branch changes the framework and the UI library, so look especially at: styled controls (date picker, calendar, toggle switch, combo boxes, scroll bars), the DataGrid on the Log page and Log › Copy, the tray, the sign-in web view, and the What's new card. Report in a "Results (date)" table as before. **Afterwards switch back:** `git checkout final` and rebuild, so the next test isn't run on the upgrade by mistake.

## MAC, 2026-11-24: branches reviewed, submission plan

- **The submission plan and a readiness table for every PR are at the top of `.claude/pr-drafts/README.md`.** Order: all PRs at the developer's pace, then an optional .NET 11 + Avalonia 12.2.0 upgrade on its own branch (kept out of `final`), then the "4.0 release" PR (version bump + What's new card).
- `preview` rebuilt from `final` (9224b03, version kept at 3.9.1). The #104 comment still links the older build: change it only with the user's OK.
- **Don't push old local branches.** `combined` and `final-next` reappeared on the fork (pushed from an old local copy) and were removed again; their content is in `final` and in `refs/archive/`. Push only `final` (and a `pr/*` branch when asked). Delete local copies with `git branch -D combined final-next` if they are still there.
- Nothing is waiting on the PC.

## DONE (PC, 2026-11-23 late): the .mp4 file-name check passed (`final` 3607502, built 4.0.0)

Run on the PC with the original-stream option on (Settings > Download > "Keep the original stream file"; the dialog has no separate "Disable conversion" box in this build, the setting is in Settings). `Backup`/`Restore` worked; auto-download was off during the run and is back on.

| Step | Result | Evidence |
|---|---|---|
| 1 | PASS | With the option on, the Settings file-name preview and the download dialog's suggested name both end in `.ts`; the dialog has no crop section |
| 2 | PASS | Renamed to `.mp4`: no error, Download was accepted and queued |
| 3 | PASS | A 160p download of a 3 h 12 min VOD (342 MB) finished as "Done": a real MP4 (`mov,mp4`, h264 + aac, 284x160, 11552 s) |
| 4 | PASS | A name ending in `.mkv`: the file name box turns red with an error icon, the tooltip starts "Filename must end with one of the supported ...", and nothing is queued |

Use 160p for this kind of check: with conversion off a crop is not possible, so a full VOD downloads. The error log shows only the known "Failed to create ShellLink object" notification message. The test file was deleted afterwards. The original instructions are kept below.

### Original instructions: one small PC check, about 5 minutes

`final` (c6e6684, CI green) now accepts **.mp4** as the file name when "Disable conversion" is on (newer Twitch VODs are MP4 inside). This came from comparing mpeter50's fork. PR branch: `pr/fix-mp4-unconverted`.
1. In the download dialog, tick "Disable conversion" (or turn it on in Settings › Download). The suggested name ends in `.ts`, as before.
2. Change the name to end in `.mp4`. No error, and Download is allowed. A 1-minute crop isn't possible with conversion off, so pick a short VOD or a clip-length one, or cancel after a few seconds and only check there's no name error.
3. If it finishes, the `.mp4` plays in a normal player.
4. A name ending in `.mkv` with conversion off still shows an error (only .ts and .mp4 are allowed).
Report as before. Add files by name, not `.claude` as a whole.

## STATUS IN ONE GLANCE (PC, end of 2026-11-23)

- **The .NET 11 + Avalonia 12.2.0 branch had one problem (R14: `--minimized` showed the window); it is fixed on the branch (ab41308), see the section at the top. Nothing is waiting on the PC** (the .mp4 file-name check also passed). Both automated PC runs are done and passed: the first (What's new card, #31 HttpClient, #109 setting, CLI) found and fixed one bug, and the second (System.Text.Json swap) passed with no fixes. Results are in 1a and 1b below.
- **`final` is the tested version.** Everything in it has been run on the PC: the What's new card and its centring (4c024ad), the #31 HttpClient token fix (68c68bd), the System.Text.Json swap (df8f791) and the `.mp4` file-name change (c6e6684). The PC checked it at 3607502; later commits only touched notes and PR text. Local `final` and `origin/final` match.
- **Open items, none urgent:** emote pictures are now confirmed; still not run on the PC are the Windows toast notifications (not a requirement) and the ffmpeg-failure half of the #109 check.
- **`preview` is behind `final`.** It was rebuilt at 9a08fcf, before the #31 token fix, the card centring, the System.Text.Json swap and the `.mp4` change. If the developer is pointed at a `preview` build again, rebuild it from `final` first (recipe in `.claude/pr-drafts/README.md`) and ask the user before changing the #104 comment's build link.
- **No PR is open for #31, the card, the JSON swap or the `.mp4` change (`pr/fix-mp4-unconverted`, ready on the fork).** The user said to leave #31 queued (last wave). Do not open any PR without the user saying so.
- **Repo hygiene.** `.claude/handoff-nudge-state.json` is a local counter file, not part of the project: do not `git add -A .claude` (it was committed once by mistake in 47e8c2c and untracked again in 4dca59e; it is now in the local `.git/info/exclude`). Add files by name.

## START HERE ON THE PC (updated 2026-11-23 evening, Mac)

### 1. Get the current code

```
git fetch origin --prune
git checkout final
git pull
```

Then `dotnet build` (close TwitchLeecher first). Or download the `final` artifact from the latest Build run on `final`. Your old local `final-next`, `combined` and `combined-all` branches are archived on the fork (see the Branch map), so don't use them.

### 1a. PC RUN of the System.Text.Json swap (DONE 2026-11-23, late, on the PC): PASS

Run on `final` 7c39ea1 (built 4.0.0). `Backup` and `Restore` worked, watched-search auto-download was switched off during the run and is back on. No fixes were needed. The original instructions are kept below the table.

| Step | Result | Evidence |
|---|---|---|
| R1 | PASS | Settings > General: "Signed in as ...", "Check for updates" ticked |
| R2 | PASS | Broadcasts (8), clips (7) and a pasted video link all returned cards with thumbnails |
| R3, chat as HTML + details | PASS | 1-minute crop (06:00:00-06:01:00 of a 13 h VOD): `.mp4` 57.6 MB, `.chat.html` with the 2 messages, `.info.json` with its fields (channel, cropStart, cropEnd, id, length, quality, title, url, ...) |
| R3, chat as JSON | PASS | `.chat.json` with `messages` and `video`; each message has `offsetSeconds`, `timestamp`, `name`, `text` and `fragments` (2 messages) |
| R3, chat as text | PASS | `.chat.txt` with the same 2 messages as `[hh:mm:ss] name: text` |
| Emote pictures (open check from the last run) | **PASS** | Audio Only, 20-minute crop (06:00:00-06:20:00), chat as HTML with emotes: 132 messages and 44 `<img>` tags with `static-cdn.jtvnw.net/emoticons` URLs; the first one returns HTTP 200 `image/png` |
| R4 | PASS | A 7-second clip finished: 6.7 MB `.mp4`, h264 + aac, 7.55 s |
| R5 | PASS | An uncropped VOD started (0%); **Cancel** ended as "Canceled", not an error |
| R7 | PASS | `tl search <channel> --last 8` listed 8 videos, exit 0 |
| Update check | PASS | With "Check for updates" on, the app was restarted: the only new error log is the known "Failed to create ShellLink object" notification one. No update notice appeared (expected: 4.0.0 against GitHub's 3.9.1) |

Notes from the run:
- **A VOD's quality list changed during the day, not because of this change.** The same VOD showed 1080p60 at the top in the morning and now shows "1440p60 (2560x1440) HEVC" first. The build just before the JSON swap (`9c8a083`) lists it too, and a plain request without sign-in does not, so it comes from Twitch (or the new sign-in token), not from the JSON code.
- **Audio Only changes the download dialog layout** (the Compress option goes), so fill in crop, file name and chat **before** choosing Audio Only. A cheap way to sample a lot of chat is Audio Only with a 20-minute crop (about 26 MB).
- **R17 (logout) was not repeated.** The sign-in from the last run is still in use.

#### Original instructions for this run

`final` (df8f791, CI green) now reads every Twitch and GitHub response with .NET's built-in JSON instead of Newtonsoft.Json. A slip would show as a search, video, clip, chat or sign-in that quietly returns nothing, so rerun part of the automated run below:
- **R1–R5 and R7**, exactly as in 1b: sign-in check, search (VODs, clips, a pasted URL), a 2-minute crop with chat as HTML plus video details, a clip, Cancel, and `tl search`.
- **Plus:** the same 2-minute crop with chat **as JSON** and **as text**. Both files should have the messages and timestamps.
- **Plus:** the update check, which now reads GitHub's release list with the new JSON code. Make sure Settings has "Check for updates" on, restart the app, and confirm `H Errors` shows no new error log. No update notice is expected: the build is 4.0.0 and GitHub's latest is 3.9.1.
- **Plus, if you can find one:** a VOD with a busy chat, to finally confirm the emote pictures in the HTML chat (open check from the last run).

Same rules as 1b: `Backup` first, never type a login, short crops, `Restore` at the end (but not after a logout test). Report as before, in a "Results (date)" table, and update the Testing lines of `bodies/system-text-json.md` and `bodies/replace-webclient.md`.

### 1b. AUTOMATED PC TEST RUN (DONE 2026-11-23 on the PC; results and notes below). Kept as the recipe for the next run

**PC Claude runs every pending check below by itself, then reports.** Nobody needs to watch, except for the sign-in in step R1. Rules:
- **Drive the app with computer use** (screenshots, clicks, typing), and use `test-scripts\pc-test-helper.ps1` for everything that doesn't need the mouse. Run `powershell -ExecutionPolicy Bypass -File test-scripts\pc-test-helper.ps1` with no arguments to see its help.
- **Never type a password or a Twitch login.** If a step needs signing in, ask the user to do it in the app window, then carry on.
- **Don't download anything big.** Use 1–3 minute crops. Pick the channel from the user's favourites in Settings, or ask the user once for a channel they watch. Never write a streamer's name into the repo.
- **Evidence for every check:** what you saw, plus a screenshot saved as `%TEMP%\tl-test\<step>.png` (only report the path; do not commit screenshots).
- **On a failure:** note it, collect `pc-test-helper.ps1 Errors` and the download's log text, and **carry on** with the other steps. Fix nothing during the run.
- **Finish with `Restore`**, even if steps failed, so the user's settings are back as they were.
- **The helper script was written on the Mac and has never run** (no PowerShell there). If one of its commands fails, fix the script (it is test tooling, not app code), commit the fix to `final`, note it in the report, and carry on. **Run `Backup` first and check it worked before anything else**: it is what makes the run safe for the user's settings.

**Setup**
```
git fetch origin; git checkout final; git pull
Get-Process TwitchLeecher -ErrorAction SilentlyContinue | Stop-Process -Force
dotnet build TwitchLeecher\TwitchLeecher.sln -c Release
powershell -ExecutionPolicy Bypass -File test-scripts\pc-test-helper.ps1 Backup
```

**The run.** "H" means `powershell -ExecutionPolicy Bypass -File test-scripts\pc-test-helper.ps1`.

| Step | Setup | Do and check | Pass when |
|---|---|---|---|
| R1 | `H Start` | Settings › General shows the signed-in account. If not, **ask the user to sign in**, then continue. | Account shown, no "sign-in has stopped working" prompt (#31 token check) |
| R2 | — | Search a channel (VODs), then switch to clips; paste a VOD URL | Results and thumbnails load both times (#31 API) |
| R3 | — | Download one VOD cropped to 2 minutes, with chat as **HTML with embedded emotes** and video details on | Shows Done; `.mp4`, `.chat.html` and `.info.json` exist; open the `.chat.html` and the emotes show as pictures (#31 Content-Type) |
| R4 | — | Download one clip | Shows Done; the file plays (#31 streamed clip) |
| R5 | — | Start an uncropped VOD download, wait about 10 s, press Cancel | "Cancelling", then "Canceled" within a few seconds, **not** an error |
| R6 | — | Settings › Download: "Keep the unconverted video if conversion fails" is **ticked**. Untick it, save, `H Stop`, `H Start`, check again | Ticked by default; stays unticked after a restart (#109). Tick it again and save afterwards. |
| R7 | `H Cli -CliArgs "search <channel> --last 3"` | Read the output | 3 videos listed, `exit code: 0` (#31, CLI) |
| R8 | `H Cli -CliArgs "--bogus"` | — | `exit code: 2` |
| R9 | `H SetLastVersion` then `H SetTheme -Theme New` then `H Start` | The What's new card appears over the window | Card shows: "What's new in 4.0", 8 items, **purple** icons |
| R10 | — | On the card: click **Full release notes** | The browser opens `…/releases/tag/v4.0.0` (a 404 page is expected). Then press **Esc**: the card closes. |
| R11 | `H Stop` then `H Start` | — | The card does **not** appear again |
| R12 | `H SetLastVersion` then `H SetTheme -Theme Original` then `H Start` | The card appears | **Gold** icons, readable text. Press **Enter**: it closes. |
| R13 | `H SetLastVersion` then `H SetTheme -Theme Coyote` then `H Start` | The card appears | **Blue** icons, red "Got it" button, readable text. Click **Got it**. |
| R14 | `H SetLastVersion` then `H Start -Minimized` | Wait 5 s: no card and no window. Then open the app from the tray icon. | The card appears only once the window opens |
| R15 | `H Stop` then `H FreshInstall` then `H Start` | Close any first-run prompts | **No** What's new card (fresh install) |
| R16 | `H Stop` then `H Restore` then `H Show` | — | Your original Version and Theme are back |
| R17 | Logout test (do last, it needs the user): ask the user to log out and sign in again | — | Both work (#31 revoke request) |

**Report:** add a "Results (date)" table under this section (step, pass/fail, one line of evidence), commit it to `final` as "Handoff: automated PC test results", push, and give the user a short summary. Then:
- Record what passed in the Testing lines of `.claude/pr-drafts/bodies/replace-webclient.md` (R1–R5, R7, R17) and `feature-requests` (#109: R6).
- Mark the What's New checks (R9–R15) in section 3 below.

### Results of the automated PC test run (2026-11-23, PC Claude, `final` 90bcdc7)

Backup and Restore both worked (settings, theme and version back as before; watched-search auto-download was switched off during the run and is back on). The helper script ran without any fix. Screenshots are in `%TEMP%/tl-test/` (R1, R2, R4, R9, R12, R13, R15 as `.png`; not committed).

| Step | Result | Evidence |
|---|---|---|
| R1 | PASS | Settings > General shows "Signed in as ..." and no sign-in prompt appeared |
| R2 | PASS | A channel's broadcasts (8), its clips (7) and a pasted `twitch.tv/videos/<id>` link all returned cards with thumbnails |
| R3 | **FAIL, then PASS after the fix (68c68bd)** | First run: the download dialog for a VOD never opened (`400 (Bad Request)` in `ApiService.GetPlaylistInfo`, see "Bug found"). After the fix: the dialog opened and the filmstrip loaded; a 2-minute crop with chat as HTML with emotes and video details finished (`.mp4` of exactly 120.0 s, `.chat.html` with the chat lines, `.info.json` with its fields). **Not confirmed:** the emote pictures. The 2 sampled minutes of chat, and a second 1-minute sample at 1:30:00, had no emotes (0 `<img>` tags), so only the plain-text part of the HTML chat was checked |
| R4 | PASS | A 7-second clip finished ("Done"): a 6.7 MB `.mp4`, h264 + aac, duration 7.55 s |
| R5 | PASS (after the fix) | An uncropped 3 h VOD started (0%, about 12 MB/s); pressing **Cancel** after about 10 s showed "Canceled" straight away, not an error |
| R6 | PASS | The setting was ticked, I unticked and saved it (`KeepTsOnFailure` false in `config.xml`), restarted, and it was still unticked. Ticked and saved again afterwards |
| R7 | PASS | `tl search <channel> --last 3` listed 3 videos, exit code 0 |
| R8 | PASS | `tl --bogus` printed the usage hint, exit code 2 |
| R9 | PASS | What's new card shown in Dark: "What's new in 4.0", 8 items, purple icons |
| R10 | PASS | **Full release notes** opened a "Page not found - GitHub" page in Chrome (the expected 404). Esc closed the card |
| R11 | PASS | Restarted: no card |
| R12 | PASS | Classic: gold icons, readable. Enter closed it, but only after I clicked the card; the first Enter did nothing (the card may not take keyboard focus by itself) |
| R13 | PASS | Coyote: blue icons, red **Got it** button, readable. **Got it** closed it |
| R14 | PASS | `--minimized`: no window and no card after 7 s. Opening from the tray icon showed the card |
| R15 | PASS | Fresh settings folder: the app opened on "Connect with Twitch" and no card |
| R16 | PASS | `Restore` put Theme Original and Version 4.0 back |
| R17 | PASS | Log out asked for confirmation and returned to "Connect with Twitch" with no new error in the log. The user signed in again in the in-app window (the user typed the login). Settings showed "Signed in as ..." and `tl search <channel> --last 1` returned a video, exit 0. The revoke request ignores failures by design, so this does not prove Twitch accepted the revoke. The settings backup was not restored afterwards, because the logout had invalidated the old token |

**Bug found (R3): every VOD playlist request returns 400. The #31 `HttpRequestClient` double-encodes the VOD token.**
- `ApiService.GetVodAuthInfo` (line 915) already does `Uri.EscapeDataString` on the token, because `WebClient.QueryString` sent values as they were. `HttpRequestClient.Send` now encodes every query value again, so the playlist request carries a double-encoded token.
- Checked directly against Twitch with the same request: token encoded once gives HTTP 200 (2217 bytes of playlist); encoded twice (what the app now sends) gives HTTP 400. `tl qualities <id>` fails the same way for every VOD, so this is not one bad video.
- Not affected: clips (the clip URL is built separately, and R4 passed), and the other `AddQuery` calls, which pass plain values.
- **FIXED (68c68bd on `final`, after the run, with the user's go-ahead). The idea was:** make the token reach the playlist request unescaped once, for example `webClient.AddQuery("token", Uri.UnescapeDataString(vodAuthInfo.Token))` in `GetPlaylistInfo` (line 1009), or stop escaping it at line 915 and change the `HttpUtility.UrlDecode(token)` just below. Fix it on `replace-webclient` and on `final`, re-cut `pr/replace-webclient`, then repeat R3, R5 and R17.
- Verified after the fix: `tl qualities <id>` lists the qualities (exit 0), and the app downloads VODs. Still to do: the same one-line fix on `replace-webclient` and in `pr/replace-webclient`, then R17.
- The failure was quiet in the app: clicking Download on a VOD showed no error message on screen.
- **Also fixed after the run (4c024ad):** the What's new card sat low and off to one side of the main window. It is now centred on the main window and stays centred if that window is moved or resized (placed by hand, because `CenterOwner` with `SizeToContent` uses the size before layout).
- Also noticed: the app starts minimised to the tray with the user's saved settings (`StartMinimised`), so a tester first sees no window.

### 2. What happened since your test round

- **The developer reviewed everything on 2026-11-23.** He merged **#110–#119** (10 PRs). He is happy to merge despite style differences and plans a rewrite later (#119). His comments are short and friendly; match that tone.
- **The other six (#104–#109) are rebased onto the new master and answered:**
  - #107: the chat request is now a `JObject`, with `CHAT_OPERATION`/`CHAT_QUERY_HASH` constants.
  - #108 (he wrote "LGTM"): its own scroll code was dropped, because #113 covers it. Renamed "Quick download and multi-select download (#96)".
  - #109: new setting, Settings › Download › "Keep the unconverted .ts if conversion fails". It's on by default.
  - #104: also linked to the new issue #120 (a duplicate of #103), with a comment on #120.
- **Wave 1 is open as #121–#127:** pipe fix, draggable dialogs, Avalonia 11.3.23, unused packages, CI build, Tmds.DBus pin, #85 Open folder.
- **All branches are on the new master:** fork `master` synced, `final` merged plus the #107/#109 changes, `preview` rebuilt, and all 35 `pr/*` branches rebased or re-parented. Every changed branch is CI-green.
- **The #104 overview comment is refreshed:** merged/open lists, a new preview build (run 45222297875, until 2026-12-07), and an **offer to group the ~26 remaining PRs into about 6 bigger ones** (UI refresh, search/download features, background features, export/processing, code quality, plus Native AOT and Avalonia 12 kept separate), or keep them small.
- **The email to the developer was sent** by the user (2026-11-23) with the same update and offer. The draft is in `.claude/pr-drafts/email-developer.md`.

### 3. The "What's new" card (TESTED on the PC 2026-11-23, all six checks passed, see the results table in 1b)

**The automated run in 1b covers this section.** The list below is the detail behind it.

**2026-11-23: the user checked it on the PC, and it "looks great".** If any of checks 2-6 below weren't run, they are still worth a quick go before the PR. All six checks were run on the PC (R9-R15) and passed. Two PC changes since: the card is now centred on the main window and stays centred (4c024ad), and the first Enter did not close the card until it was clicked (keyboard focus; minor, not changed).

`final` (8d963d6, CI green) adds a one-time "What's new in 4.0" card, built from the approved mockup (Option A: https://claude.ai/artifact/6pjwnc2sXaCqdUoDUtuzyn).
- It shows once, the first time an **existing** install runs 4.0. It reads the version that last saved `%AppData%\Twitch Leecher DX\runtime.xml` (its `Version` attribute). A fresh install skips it.
- **`final`'s version is now 4.0.0**, for testing. In the upstream PR the version bump is left to the developer.
- Code: `TwitchLeecher.Gui/Views/WhatsNewWindow.axaml(.cs)` (text and `ForVersion = 4.0`), `DialogService.ShowWhatsNewIfUpgraded`, called from `MainWindowVM.Shown()`. Five Font Awesome icons were added to `IconData.cs`, plus `FeatureIconBrush`/`FeatureIconTintBrush` per theme in `App.axaml` (Coyote uses its blue `#45C7D9`).

Checks:
1. Build `final` and run it with your existing settings. The card shows once, over the main window: "What's new in 4.0", 8 items, frameless, and you can drag it by the card.
2. **Full release notes** opens `https://github.com/schneidermanuel/TwitchLeecher-Dx/releases/tag/v4.0.0` (a 404 until that release exists, which is expected). **Got it**, Enter and Esc all close it.
3. Close and reopen the app: it does **not** show again.
4. To see it again: close the app, set `Version="3.9.1"` in `runtime.xml`, and start the app. Do this once each in Dark, Classic and Coyote (Settings › General › Theme): icons are purple, gold and blue respectively, and the text is readable.
5. Started minimised to the tray: the card waits until you first open the window.
6. Fresh install: rename the `Twitch Leecher DX` folder in `%AppData%`, start the app, and no card should appear. Then put the folder back.

### 3a. Quick look: the CLI on the Info screen (DONE 2026-11-23, PC: "all looks good")

`final` (922627e, CI green) adds a "Command-line tool" section to the Info screen, between Source Code and Donate, and a matching section in the README. On the PC the product title on the Info screen was also centred vertically against the logo (it was top-aligned); that fix is now also folded into `pr/feature-cli` (dd691a4, CI green). Open Info and check that the section reads well, the example line shows in a fixed-width font, and Donate still sits below it. The same change is in `pr/feature-cli`.

### 3c. The WebClient -> HttpClient change (#31): TESTED on the PC 2026-11-23, one bug found and fixed

**The automated run in 1b covers this section.** The list below is the detail behind it.

`final` (98b9a4e, CI green) sends every web request through one shared HttpClient: sign-in check, search, video info, playlists, clips, chat, emotes and the update check. **Every one of these needs a quick run**, because a mistake would show as a failed request:
1. Start the app while signed in: no "sign-in has stopped working" prompt (token check). Settings › General shows your account.
2. Search a channel (VODs, then clips) and paste a VOD URL: results and thumbnails load.
3. Download a short cropped VOD: it finishes, and the log shows the playlist and chunks.
4. Download a clip: it finishes (now streamed straight to disk).
5. A short download with chat as HTML with embedded emotes: the emotes show in the .chat.html (Content-Type is read differently now).
6. Start a longer download and press Cancel: it stops quickly and shows "Canceled", not an error.
7. Log out and sign in again (the revoke request).
8. Settings: with "check for updates" on, restart; no error (the update check uses it too).
9. `tl search <channel> --last 3` works (the CLI uses the same services).

If one fails, the log or the error text names the request. The PR branch is `pr/replace-webclient`.

**PC result:** items 1, 2, 3, 4, 6, 7 and 9 pass. Item 3 first failed (every VOD playlist returned 400: the VOD token was escaped twice), fixed in 68c68bd on `final` and in `pr/replace-webclient` (5658660, one clean commit, no AI lines). **Item 5 (emote pictures) was confirmed later the same night, see 1a.** **Not confirmed:** item 8 (the update check was not looked at on its own; there was no related error in the logs at any start). In the PR text (`bodies/replace-webclient.md`) these two are listed as not yet confirmed.

### 3b. The #109 setting (TESTED on the PC 2026-11-23)

**The automated run in 1b covers this section.** The list below is the detail behind it.

The #109 setting has only been built on CI, not run by a person. In the `final` build:
1. Settings › Download shows "Keep the unconverted video if conversion fails", ticked by default. Untick it, save, restart the app: it stays unticked.
2. Optional, with the always-failing ffmpeg trick from the #109 test: with it ticked, a `.ts` is kept; with it unticked, no `.ts` is left.

**PC result:** check 1 passed (ticked by default, stayed unticked after save and restart; ticked again afterwards). The optional ffmpeg trick (check 2) was not run. The #109 description's Testing line can say check 1 passed.

### 4. What's next (nothing to send now)

- **Decided 2026-11-23 (user): leave the #31 PR queued.** `pr/replace-webclient` (5658660) already has the token fix. No PR is open for it, and it goes in the last wave (code quality) when the user says so. Its Testing text is up to date (emotes and the update check were run in the System.Text.Json run).
- **Emote pictures in HTML chat: confirmed on the PC 2026-11-23** (see 1a: 44 emote images in a 20-minute sample).
- **The Newtonsoft to System.Text.Json swap: tested on the PC 2026-11-23 (see 1a), passed.** Its PR text (`bodies/system-text-json.md`) says what was run. No PR is open for it either.
- **Not run on the PC:** the Windows toast notifications (not a requirement), and the ffmpeg-failure part of the #109 check.

- **Done:** the Newtonsoft.Json to System.Text.Json swap (user decision, 2026-11-23): built on the Mac, tested on the PC. Details are in the PR guide.

- **Issues, frameworks and optimisations were checked on 2026-11-23:** see the PR guide, "Check 2026-11-23 (evening)". Open candidates: #31 WebClient -> HttpClient, the Newtonsoft stable pin, and Avalonia 12.2 after about 2026-12-07.
- `preview` was rebuilt from `final` (9a08fcf), kept at version 3.9.1 so the What's New card stays hidden for the developer.

- **Wait for the developer's answer** on bigger versus small PRs, and for merges of #104–#109 and #121–#127.
- Each merge of one of #104–#109 will need the others rebased (they overlap in `ApiService`/`DownloadService`).
- Then Wave 2 (UI refresh), either as 6 PRs or 1, depending on his answer. **Don't open any PR without the user saying so.**
- Before any submit, run the "no Claude references" check in `.claude/pr-drafts/README.md`. It must print nothing.

The test round below is kept as the record of what was checked.

## PC TEST ROUND (2026-11-23, DONE): `final-next`

**One round tests everything new since your last test (the `combined-all` round, finished 2026-11-20).** That includes the Ninject swap, which was never run.

### What's in `final-next`

`final-next` = `final` + 4 changes. The #85 Open folder fix and the new `tl` command-line tool (#36) are on `final` too. `final` = your tested `combined-all` + the PR guide + the Ninject swap. Each change is also its own PR branch on `master` (see "Branch map").

| # | Change | What could break | Commit on final-next |
|---|---|---|---|
| 1 | **Ninject → `Microsoft.Extensions.DependencyInjection`** (on `final` already) | A page or dialog fails to open if its class isn't registered. `ValidateOnBuild` makes most mistakes stop the app at start-up instead. All 36 registrations were checked against every constructor. | 69e245c (in `final`) |
| 2 | **ReactiveUI removed**. Three commands now use the project's `DelegateCommand.FromAsyncHandler`. `BindableBase` no longer derives from `ReactiveObject`. DynamicData's `AddRange` is a loop. | The Download button in the dialog, Settings › Undo and Settings › Defaults (the three commands that changed) | 60cc9aa |
| 3 | **Icon package replaced** by a built-in `Icon` control (`TwitchLeecher.Gui/Controls/Icon.cs` and `IconData.cs`, the same Font Awesome 6.7.2 shapes, credited in the README) | An icon missing, blank, the wrong size, off-centre or the wrong colour | 1bd2079, ca224a2 |
| 4 | **Native AOT for Windows publishes**: ffmpeg path from `AppContext.BaseDirectory`, enum descriptions, `Marshal.SizeOf<T>`, compiled bindings in the Gui project, `BuiltInComInteropSupport` removed. Plus AOT-safe JSON: watched searches use a source-generated `WatchedSearchesJson`, and VOD info and chat JSON are built as `JsonObject`. | **Only in the published native exe:** sign-in web view, ffmpeg not found (desktop notifications are not a requirement), watched searches not saving or loading, chat/VOD-info JSON | a86311b |
| 5 | **Avalonia 12.1.6** (DataGrid 12.1.2, progress ring 0.11.0, `Avalonia.Diagnostics` dropped, clipboard `using`) | Styled controls (date picker, calendar, toggle switch, combo box, scroll bars), header, tray, the DataGrid, the clipboard (Log › Copy) | cd7ad5b |
| 6 | **CI start-up check:** CI now launches the published app (Linux under a virtual display) and fails if it exits within 20 s | — | 9c8a28d |

**CI:** every `final-next` build is green (latest checked: cfbb67f, run 45171096420, which includes #85). The native Windows exe publishes and starts, and Linux builds and starts. Only 5 AOT warnings are left, from Newtonsoft.Json, System.Linq.Expressions and Microsoft.CSharp. **None of this has been run by a person.**

### 1. Two builds, because AOT only exists in the published exe

`dotnet build` gives the normal (JIT) exe, which tests changes 1, 2, 3 and 5. **Only `dotnet publish` makes the native AOT exe.** Local AOT publishing needs the Visual Studio "Desktop development with C++" tools, so use the CI artifact.

Close every copy first: `Get-Process TwitchLeecher -ErrorAction SilentlyContinue | Stop-Process -Force`.

- **A. JIT build (normal):** `git fetch origin; git checkout final-next; git pull`, then `& 'C:\Program Files\dotnet\dotnet.exe' build TwitchLeecher\TwitchLeecher\TwitchLeecher.csproj -c Release`, then run `TwitchLeecher\TwitchLeecher\bin\Release\net10.0\TwitchLeecher.exe`. The test scripts use this one.
- **B. AOT build:** GitHub → Actions → the **latest "Build" run on `final-next`** → Artifacts → `TwitchLeecher-final-next-win-x64` (artifacts are kept 14 days; push any commit to get a fresh one). Unzip it into its own folder, e.g. `C:\TL-aot\`, and run `TwitchLeecher.exe`. Check it's native: the folder has no `TwitchLeecher.dll`, and Task Manager shows no ".NET Host".
- **Traps:**
  - Switching branches doesn't rebuild the exe (`bin\` isn't tracked).
  - A copy in the tray swallows the next launch. If the app looks old, it is old.
  - Both builds share `%APPDATA%\Twitch Leecher DX`, so settings, sign-in and watched searches carry over between them. That's useful for test 7.

### 2. Checks (about 30 minutes; note PASS/FAIL and any error text)

**PC Claude can run these for you.** In the Claude desktop app on the PC, ask it to "use computer use to run the PC TEST ROUND checks in LATEST.md". It takes screenshots and clicks through the app, and it can compare icons against `final` itself.
- **You still do:** typing the Twitch password in check 10, granting it screen access when asked, and judging anything it flags as uncertain.
- **It records:** PASS/FAIL per check in a results file, and screenshots of any failures.


Do 1–12 on **build A** first, then repeat 1, 4, 5, 7, 8, 9, 10 and 11 on **build B**. Those are the ones AOT can break.

1. **Start-up:** no error. The theme applies, and it opens on Search. Switch New → Original → Coyote once.
2. **Icons (change 3):** compare against `final` or a screenshot from before. Check the header tabs and buttons (Search, Downloads, Settings, Info, Donate), the result card buttons and ★, the Downloads page (pause, play, bin), message-box icons and the watched-search bin. Each icon should match in size, centring and colour, including hover and active states, in all three themes.
3. **Styled controls (change 5):** the Custom date range picker (field, calendar button, pop-up calendar, dimmed future days), toggle switches (Hide downloaded, watched searches On/Off), combo boxes, scroll bars, the results toolbar shrinking as you narrow the window.
4. **Search:** a channel search. Results appear in the right-hand pane, with thumbnails and badges.
5. **Download dialog:** opens, the filmstrip loads, and **Download** queues it (changed command).
6. **Settings:** every side-menu section opens, especially **Watched searches** (the one registration that had been missing). Change something, then **Undo**, then **Defaults** (changed commands). Save.
7. **Watched searches save and load (AOT JSON):** add a watched search, close the app, reopen it. It's still there with its settings. On B, also check that a search added in A shows up.
8. **One short download** (3-minute crop) with **Save chat as JSON**, **Save video details** and **Compress**. The download finishes. `.chat.json` and `.info.json` open, with the same fields as before. Compression runs (proves ffmpeg is found).
9. **Log:** open the download's log, then **Copy** (clipboard API changed), and paste it somewhere. **#85:** while a download is queued or downloading, its **Open folder** button shows and opens the download folder.
10. **Sign-in:** log out, then sign in again in the in-app window (the web view is the biggest AOT unknown).
11. **Tray:** minimise to the tray, then reopen from the tray. A second launch wakes the running window. **Desktop notifications are NOT a requirement** (user, 2026-11-23: they never worked reliably on the PC; on 2026-11-23 the JIT build logged "Failed to create ShellLink object" at start-up). Don't test them or block on them; a failure there is logged and the app carries on.
12. **Command-line tool (#36):** `tl.exe` is in the CI artifact next to `TwitchLeecher.exe`. From a JIT build, run `dotnet run --project TwitchLeecher\TwitchLeecher.Cli -c Release -- <args>`. Sign in in the app first. Try:
    - `tl search <a channel you watch> --last 5`
    - `tl qualities <a VOD id from that list>`
    - `tl download <that id> --from 0:10:00 --to 0:13:00 --out C:\TL-cli`: progress shows, the file plays, and Ctrl+C on a second run cancels it.
    - `tl download <id> --quality 720p` picks 720p (or tells you which qualities exist).
12b. **Optional, broader:** `powershell -ExecutionPolicy Bypass -File test-scripts\test-combined-all.ps1 -SkipBuild` (Tier 1) on build A.
13. **What AOT is for:** with nothing downloading, compare A and B in Task Manager (memory) and time from double-click to window. Note the numbers; they go in the AOT PR description.

**Linux:** CI only proves it builds and starts under a virtual display. If you have WSL or a Linux VM, run the `TwitchLeecher-final-next-linux-x64` artifact and do checks 1, 2, 4 and 6.

### 3. If something fails

- **The log** is in `%APPDATA%\Twitch Leecher DX` (or Settings › Log). For an AOT-only failure, also run the AOT exe from a terminal to see what it prints.
- **Which branch owns it:**

  | Change | Branch |
  |---|---|
  | 1 | `replace-ninject` |
  | CLI | `feature-cli` |
  | 2 | `remove-reactiveui` |
  | 3 | `replace-icon-pack` |
  | 4 | `native-aot` (JSON parts: final only, see below) |
  | 5 | `avalonia-12` |

  Fix it on that branch and again on `final-next`, then re-cut its `pr/*` branch with `.claude/tools/make_pr.sh` (see "Tools").
- **Known fix patterns:**
  - "Unable to resolve service for type 'X'": add `services.AddTransient<X>();` in `TwitchLeecher.Gui/Modules/GuiModule.cs`.
  - A blank icon: add its name, view box and path to `IconData.cs`. The names come from the Font Awesome 6 free set.
  - An AOT-only crash in JSON: use a source-generated context or `JsonObject`.
  - An AOT-only crash elsewhere: the culprit is usually reflection, so search the log for the type name.
- **If Avalonia 12 or AOT can't be fixed quickly:** leave it out. `final` stays your working version, and the PRs are independent.

### 4. When it passes

- **Make it final:** fast-forward `final` to `final-next` (`git checkout final; git merge --ff-only origin/final-next; git push`).
- **Rebuild `preview`** from `final` (recipe in `.claude/pr-drafts/README.md`).
- **Ask the user** whether to point the #104 comment's build link at the new `preview` run. It still links run 44813082025: the earlier tested code, from before Ninject.
- **Update the PR descriptions' Testing sections** with what you confirmed: `bodies/replace-ninject.md`, `remove-reactiveui.md`, `replace-icon-pack.md`, `native-aot.md` (add the memory and start-up numbers), `avalonia-12.md`.

## Notes from the PC test run (2026-11-23), for the next run

- **Build output trap.** `bin\Release` is shared by every branch checkout. Building a PR branch (for example `master`-based, version 3.9.1) overwrites it, and the next launch runs that build with the old look and a 3.9.1 title. The helper script starts the newest `TwitchLeecher.exe` it finds. **Rebuild `final` and check the title says 4.0 before testing.**
- **The app starts minimised to the tray** when the user's saved settings say so (`StartMinimised`). The window then only appears from the tray icon (click the arrow by the clock, then the purple icon). A copy left running also blocks every other launch and locks `bin\`.
- **The startup entry follows the last exe that ran** (the app rewrites it at each start), so Windows may open an old build at boot.
- **Watched searches auto-download while the app runs.** The test run set `"Enabled": false` in `watched_searches.json` after `Backup`, and `Restore` put it back. Keep doing that, or a test can queue real downloads.
- **Computer use.** Ask for access with the exe basename `twitchleecher.exe` (the display name did not match a running copy). The window opens small; the maximise button is at the top right of its title bar. Pages change about a second after a click, so wait before a screenshot.
- **Log out invalidates the old sign-in.** After a logout test do **not** run `Restore`: it would bring back a revoked token. Sign in again, then delete `%TEMP%	l-test-backup`.
- **Cause of the VOD 400, for reference.** `ApiService.GetVodAuthInfo` returns the token already URL-escaped, and `HttpRequestClient.AddQuery` escapes every value again. Any new code that passes an already-escaped value to `AddQuery` has the same problem. Pass plain values.
- **The helper script worked unchanged** (`Backup`, `Restore`, `SetLastVersion`, `SetTheme`, `FreshInstall`, `Start`, `Stop`, `Cli`, `Errors`, `Show`).

## Branch map (2026-11-23 evening)

| Branch | What it is |
|---|---|
| `master` | Synced with upstream (24cab52, includes #110–#119). |
| `final` | **Your working version.** Everything tested, plus the new master, plus the #107/#109 review changes. Holds this handoff, `.claude/pr-drafts/`, `.claude/tools/` and `test-scripts/`. |
| `upgrade/net11-avalonia12.2` | **Optional upgrade, not in `final`.** `final` (b4226ab) + the .NET 11 / Avalonia 12.2.0 commit + the `OnSignedIn` fix (ab41308). Tested on the PC 2026-11-23/24 (all of R1-R16 passed after the fix). Needs the .NET 11 SDK (installed on the PC). Offer it after everything else has merged. |
| `preview` | One clean commit on `master` with `final`'s code (no notes, scripts or AI lines). Linked from the #104 comment. Rebuild it whenever `final` changes. |
| `*-pr` (16) | Heads of upstream PRs #104–#119. #110–#119 are merged; #104–#109 are open and rebased. |
| `pr/*` (35) | Clean PR branches, all on the new master. Wave 1 is open upstream as #121–#127. `pr/_after-open-prs`/`-theme` are the stand-in base for Wave 3 (master + #104–#109 as they were before the review changes). |
| Not in `final`, never run | `improve-error-logging`, `replace-webclient` (#31), `add-unit-tests`, `refactor-results-viewmodel`, `claude/refactor-shared-styles-35c0a9`, `tray-tooltip-events`, `claude/fix-slow-downloads-15ccca` (on hold), `deps-security-notifications` |
| `refs/archive/*` (41) | Old working branches (`final-next`, `combined`, `combined-all`, the `claude/*` UI branches and others). Get them with `git fetch origin 'refs/archive/*:refs/archive/*'`. |

**Final-only AOT work:** the JSON fixes for watched searches, VOD info and chat live only in `final`. When those feature PRs are re-cut, carry the matching part over so each one is AOT-safe on its own.

## Upstream status and PR plan

- **Merged:** #110–#119. **Open:** #104–#109 (rebased, review changes done), #121–#127 (Wave 1). New issue #120 is covered by #104.
- **Overview comment:** https://github.com/schneidermanuel/TwitchLeecher-Dx/pull/104#issuecomment-7049853680 (refreshed 2026-11-23). Check first: `gh pr list --repo schneidermanuel/TwitchLeecher-Dx --author bluefroguk1 --state all`, plus the comments on each open PR.
- **Waiting for the developer** to choose between bigger grouped PRs and small ones. The full plan and history are in `.claude/pr-drafts/README.md`.

## Upstream issues: what's covered (checked 2026-11-23, 19 open)

| Issue | Covered by |
|---|---|
| #35 size estimate | #119 |
| #37 clips | #106 |
| #48 HEVC/AV1 | #105 |
| #49 keep .ts | #109 |
| #50 VOD info file | #117 |
| #56, #87, #92, #103 | #104 |
| #71 chat | #107, plus the chat-export branch (HTML/JSON) |
| #86 Favourites default | #118 |
| #96 QOL | #108 |
| #31 async/HttpClient | **Partly:** `replace-webclient` (untested, not in `final`) |
| #85 "Open folder" on queued/downloading items | **Fixed 2026-11-23:** `feature-85-open-folder`, clean copy `pr/feature-85-open-folder` (on `master`, Wave 1 size). Also in `final` and `final-next`. Not run yet. |
| #102 only "Audio Only" offered for one VOD | **Can't reproduce:** that VOD (2876332302) no longer exists (Twitch returns no playback token). Needs a fresh example. |
| #25, #38 download buttons do nothing (2024, versions 3.5/3.6, no logs) | Not reproduced. Likely fixed by later releases and #104, but unconfirmed. Could ask the reporters to retry the `preview` build. |
| #36 CLI | **Built 2026-11-23:** `feature-cli`, clean copy `pr/feature-cli` (stacked on replace-ninject). Also in `final` and `final-next`. `tl search`, `tl qualities` and `tl download` work as described in check 12. CI only checks help, usage and the no-sign-in exits. |
| #42 hover preview | **Ignore (user, 2026-11-23):** superseded by how this build handles previews (the download dialog's preview filmstrip). |

## Tools (`.claude/tools/`, run from Git Bash in the repo)

- `make_pr.sh <pr-branch> <target-base> <source> <source-base> <message-file>` squashes `source-base..source` into one clean commit on `target-base`. It leaves out `test-scripts/`, `.claude/` and AI lines. Write the message file without any `Co-Authored-By` line.
- `ci.sh <ref> <name>` pushes `<ref>` (plus the `pr/ci-build` workflow if it has none) as `ci-check/<name>` so Actions builds and start-up-checks it. Delete the branch afterwards (`git push origin --delete ci-check/<name>`).
- The edit scripts used for this round (`ninject.py`, `reactiveui.py`, `icons.py`, `aot.py`, `stj.py`, `avalonia12.py`) were one-offs on the Mac and are already applied. They aren't needed again.

## Rules (from the user, keep them)

- **Never name, link or credit the third-party Twitch downloader project used for ideas.** Code, comments, commits, PR text, docs.
- **Each change is its own branch and its own upstream PR**, so the developer can pick and choose. Don't bundle.
- **PR branches and PR text must contain no reference to Claude or any AI**: no `Co-Authored-By` line, no "Generated with" line, no `.claude/` files, no test scripts. (Working branches still carry the `Co-Authored-By: Claude Opus 5.5` trailer; the recipe below strips it.)
- **PR "Testing" sections claim only what the user actually confirmed** (see the results below). Do not invent coverage.
- Commit freely on working branches. Pushing working, `pr/*`, `final*` and `ci-check/*` branches to the fork is routine (the user asked for everything to be synced there). **Opening PRs or commenting upstream only when the user asks.** Never commit to `master`.
- Ask before merging branches into each other or opening PRs to upstream.
- Mark known limits in code as `// shortcut: <limit>, <when to upgrade>`.
- **Views use theme brushes only** (`{DynamicResource <Key>Brush}`), never hex colours (translucent black overlays and the red LIVE badge are the allowed exceptions).
- **A theme must define every redesign key twice: as a `Color` AND as a literal `SolidColorBrush`** (see "Lessons"). New themes must do the same.

## Project and PC setup

- C# .NET 10, Avalonia 11.3.23 on `final` (12.1.6 on `final-next`), Microsoft.Extensions.DependencyInjection (Ninject is gone), CommunityToolkit.Mvvm, no ReactiveUI on `final-next`, icons from the built-in `TwitchLeecher.Gui.Controls.Icon`. Solution in `TwitchLeecher/`; app project `TwitchLeecher/TwitchLeecher/TwitchLeecher.csproj`. Fork `bluefroguk1/TwitchLeecher-Dx`; upstream `schneidermanuel/TwitchLeecher-Dx`. Design canvas: https://claude.ai/artifact/CYBdwo46oR2LqydWnUdo62
- **.NET SDK is at `C:\Program Files\dotnet\dotnet.exe`, not on PATH by default** (an x86 `dotnet` without an SDK comes first). Build: `& 'C:\Program Files\dotnet\dotnet.exe' build TwitchLeecher\TwitchLeecher\TwitchLeecher.csproj -c Release` (0 errors is the bar; ~20 warnings are normal).
- Run `bin\Release\net10.0\TwitchLeecher.exe` (not `dotnet TwitchLeecher.dll`, which shows as ".NET Host"). That is the JIT build. The native AOT exe only comes from `dotnet publish -r win-x64` (needs the VS C++ tools) or the CI artifact.
- **The app is single-instance.** A hidden/tray instance, or one left by a test script, blocks new launches and locks `bin\` so builds fail with "file is locked". Kill every `TwitchLeecher` process (`Get-Process TwitchLeecher | Stop-Process -Force`) before building or launching.
- Terminal tool: `run_in_terminal` types literal lines only. Start scripts with `$env:Path = 'C:\Program Files\dotnet;C:\Windows\System32;C:\Windows;C:\Windows\System32\WindowsPowerShell\v1.0;C:\Program Files\Git\cmd'; powershell -ExecutionPolicy Bypass -File test-scripts\<script>.ps1 -SkipBuild`.
- **App settings are shared by every branch** (`%APPDATA%\Twitch Leecher DX\config.xml`). The user's saved Theme is "Original" (the code default is "New"); Quick download and similar toggles persist between branches and change behaviour (e.g. "Disable conversion" hides the crop section). Check them when something looks wrong.
- ffmpeg on `master` is the old committed 5.0. PR #110 replaces it with 9.0.2 fetched at build time (the 9.x exe is over GitHub's 100 MB file limit).

---

# HISTORY (reference only, older rounds; the sections above win where they disagree)

## Where things stand (as of 2026-11-19)

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
- **Header buttons:** Info / Donate are icon-only so the header still fits at the 780px minimum width. Log out moved to Settings › General (round 3).

## PC session 2026-11-20 (late): what changed in combined-all since round 3, and branch sync status

**New in `combined-all` (all user-tested on Windows):**
- Search results show in the **right-hand pane** of the Search page (skeleton loading, "No videos found", empty state).
- Own date picker and calendar themes (`TlDatePickerTheme`, `TlCalendarItemTheme`, calendar styles in `TwitchLeecher/App.axaml`); future days are dimmed (blackout dates added in `TemplateApplied`), Show pills in one row.
- Watched searches rows rebuilt (capped text widths, controls line: When found / Active / Edit / bin).
- Results toolbar: **always one row**, shrinks in stages from `SizeChanged` (`tight` below 820 = Watch and Hide downloaded lose their labels, `tiny` below 620 = summary hides, title and sort box shrink), animated with `DoubleTransition`. Window `MinWidth` is 880. Sizes and padding must come from styles, not attributes on the element (a local value beats a style and blocks the animation).
- Header order Donate, Settings (icon only), Info. Downloads tab count pill hidden when the queue is empty, Pause all greyed out when nothing can be paused, no "Download added" pop-up, Download schedule wording, Twitch logo and the user's avatar in Settings (avatar needs `GetChannelInfo`, so it only works with `in-app-signin` and the API branch together).
- Tray tooltip: implemented event-driven on `tray-tooltip-events`; the polling version in `d78f09e` is superseded, do not apply it.

**Branch sync (local only, nothing pushed):** the fixes above were applied as new commits on top of each branch, each built with 0 errors:
`ui-header-nav` (6274e6a), `ui-downloads-queue` (0d513df), `ui-download-dialog` (ab1973f), `ui-settings-sections-v2` (eafb37b, Theme on one line only), `ui-watched-searches-in-settings` (5c2f36d), `ui-results-toolbar` (9719b13), `ui-search-sidebar` (8af47dd), `ui-dark-inputs` (df2d788, calendar styles), `in-app-signin` (1256a81). All branches are named `claude/<name>`.

**Not applied to individual branches (live only in `combined-all`):** the Settings Twitch logo and avatar (`b0ec29e`, `b96a4d1`, needs `GetChannelInfo`), the Download schedule wording `1da71bc` (belongs on `feature-download-schedule`; no local copy of that branch), the tray tooltip.

**Before pushing:** origin copies of several of these branches differ from local, so check `git log origin/<branch>..<branch>` and use `--force-with-lease` only for rebased ones. Squash stray `.claude/handoff-nudge-state.json` commits in `improve-error-logging` and `tray-tooltip-events` when cutting PR branches. Expect conflicts between `refactor-shared-styles` and `refactor-results-viewmodel`. `fix-slow-downloads` showed no measured gain, do not claim a speed-up. Windows toast notifications are still unverified.

## TEST PLAN FOR combined-all (done 2026-11-20; kept for reference, the 2026-11-23 round above comes first)

`combined-all` holds **every change** (the 16 open PRs plus all new work) and **compiles on GitHub Actions** (Windows + Linux, run 44752247971 at commit dfccddd). It has **not been run yet** in this form, so this round is about finding cross-change bugs before any more PRs go upstream.

**Round 3 UI tweaks (from the user's screenshot, in combined-all and the matching `pr/*` branches; the test script covers them in Tier 1):**
- **Header:** no Log out button. Clicking the logo or "Twitch Leecher" goes to Search. (`ui-header-nav`; on its own, the old Settings page shows "Signed in as …" + Log out.)
- **Settings › General:** TWITCH ACCOUNT "Signed in as <login>" + Log out, then THEME. The Appearance section is gone. The Windows start-up label is now "Start Twitch Leecher when Windows starts". (`ui-settings-sections`, `start-with-windows`.) The login comes from the token validation (`IApiService.SignedInLogin`, then `IAuthService.UserName`, then `MainWindowVM.UserName`), so no extra API call.
- **Settings › Download:** the Advanced expander is gone. TEMPORARY FOLDER and MAX CONNECTIONS sit with the other fields.
- **Settings › Watched searches:** an On/Off switch (ToggleSwitch, accent colour) replaces the tick box. Everything below it is disabled and dimmed while Off. (`watched-searches`.)
- **Date picker:** its text was top-aligned because the picker's inner TextBox takes `VerticalContentAlignment` from the picker, which defaults to Top. It's now centred, and the pop-up calendar is themed (`ui-dark-inputs`). The sidebar's Custom range blocks future dates, and To can't go before From (`ui-search-sidebar`). Other text boxes were already centred.
- **Note:** the user's screenshot still showed the ★ by the Channel box, which was removed in round 2. They were on an older build, so take the latest `combined-all`.

**1. Get the build:**
- **Quickest:** GitHub → Actions → latest "Build" run on `combined-all` → Artifacts → `TwitchLeecher-combined-all-win-x64`. Unzip it into `TwitchLeecher\TwitchLeecher\bin\Release\net10.0`, then run the script with `-SkipBuild`.
- **Or build it:** `git fetch origin; git checkout combined-all; git pull`, then let the script build. It uses `C:\Program Files\dotnet\dotnet.exe`.

**2. Close every running copy first:** `Get-Process TwitchLeecher | Stop-Process -Force`.

**3. Run ONE script, which walks through everything with the right screen locations:**

```
powershell -ExecutionPolicy Bypass -File test-scripts\test-combined-all.ps1            # build + Tier 1 and 2
powershell -ExecutionPolicy Bypass -File test-scripts\test-combined-all.ps1 -SkipBuild -Tier 3   # long downloads, later
powershell -ExecutionPolicy Bypass -File test-scripts\test-combined-all.ps1 -SkipBuild -Tier 4   # waiting / reboot, later
```

| Tier | Needs | Covers |
|---|---|---|
| 1 | nothing (~25 min) | process name (auto); in-app sign-in with sub-only; opens on Search with logo + version; header (no Sub-only, no Log out, logo → Search); date picker + centred text; Settings account / Theme in General / no Advanced / watched On-Off switch; search sidebar, Links/IDs box, Search tab → form; results toolbar, channel ★, Clips; Settings side menu and every new toggle; New/Original/Coyote themes on every page; dialogs centred + draggable; second launch wakes the app; tray first-minimise prompt; watched-search rows, bin, first run; **idle CPU (auto)** |
| 2 | one 3-minute download (~20 min) | download dialog (filmstrip, editable name, Extras); Downloads page speed / pause / resume; **chat JSON (auto check)**, VOD info (auto), **compression to HEVC + no leftovers (auto)**, encoder in log; chat HTML; badges QUEUED → DOWNLOADED by themselves; Download Selected; desktop notification; "This video is for subscribers only."; clip download |
| 3 | an hour of a busy VOD's chat (optional) | parallel chat speed; CPU vs graphics-card compression |
| 4 | waiting / a live channel / a Windows sign-out (optional) | off-peak schedule; live stream held back; start with Windows; token stopped working → prompt |

**4. Send back** the results file the script saves in `%TEMP%` (`twitchleecher-combined-all-*.txt`). Each FAIL carries your one-line note.

**Most likely to break**, because these are places where several changes meet:
- **In-app sign-in:** never run on Windows. It uses a web view.
- **Desktop notifications:** in round 1 no toast ever appeared.
- **Settings › General:** it holds tray, start-with-Windows and notification toggles from three branches.
- **Results page:** results toolbar, quick-download service, channel ★ and the new badge refresh all meet there.
- **Download dialog Extras:** chat formats, video details and compression from three branches.

**About the other `test-scripts/*.ps1`:** each one is for **re-testing a single PR branch on its own**, before submitting that PR. Several describe that branch's own screens, not `combined-all`. For example, `test-ui-header-nav.ps1` expects the old Settings page, and `test-login-and-icon.ps1` tests the old browser login, which is not used any more. Don't use them for this round.

## PR preparation and optimisation review (2026-11-21), READ THIS BEFORE SUBMITTING ANYTHING

**PRs:** 22 clean, single-commit `pr/*` branches are prepared and pushed to the fork. **None are submitted.**
- **Clean:** no test scripts, no `.claude/` notes, no AI references.
- **Compiled:** each one built on GitHub Actions.
- **Where to look:** `.claude/pr-drafts/README.md` has the waves, dependencies, test status, how to submit and the "no Claude references" check. Descriptions are in `.claude/pr-drafts/bodies/`, and the overview for the developer is in `.claude/pr-drafts/overview-issue.md`.
- **Not prepared:** slow-downloads (no measured gain) and login-page-and-icon (superseded by the in-app sign-in).

**Optimisations 1-3 are now BUILT** (2026-11-21), each on its own working branch with a test script, and as clean `pr/perf-*` branches. They're merged into `combined-all` (build run 44721562021 passed) but not run yet:
- `perf-downloads-idle` (`test-perf-downloads-idle.ps1`, needs one short download). The Downloads page tick runs only while something downloads; idle CPU is measured automatically. In `combined-all` the watched-searches count refreshes on its own event.
- `perf-result-badges` (`test-perf-result-badges.ps1`, needs one short download). Cheaper badge refresh, and **DOWNLOADED now appears when a download finishes** (a bug fix).
- `perf-parallel-chat` (`test-perf-parallel-chat.ps1`, needs a 1-hour chat download). Chat is fetched in 10-minute segments, 4 at once.

**Optimisation opportunities found in `combined-all`, biggest first (1-3 built, 4-6 not):**
1. **Downloads page timer:** it ticks every second for ever, even with nothing downloading and the page closed. That's a small but constant idle wake-up, which is what the original "resource heavy" complaint was about. Run it only while a download is active (and the page is open).
2. **Results badges:**
   - **Cost:** every time the download queue gains or loses an item, the results page rebuilds every result's default file path and checks the disk (`File.Exists`) and the queue (linear) for each one, on the UI thread. That's O(results × downloads) per change.
   - **Bug:** the DOWNLOADED badge doesn't update when a download *finishes*, because only add/remove triggers it.
   - **Fix:** compute off the UI thread, cache paths, and react to download state changes.
3. **Chat download is one request at a time:** a long VOD's chat takes thousands of sequential GQL pages. Splitting the time range into 4–8 segments fetched in parallel (then merged and de-duplicated) would make it several times faster.
4. **Image loading shares one 4-slot queue:** the download dialog's preview sprite sheets wait behind search-result thumbnails, and all sheets load at once. Give previews their own priority and load sheets as the filmstrip needs them.
5. **Watched searches:** each check makes one Helix `/streams` call per Broadcast search. `/streams` accepts up to 100 `user_login`s, so one call per check would do.
6. **Old network code:** `WebClient` (27 uses, obsolete, blocks a thread per request) and `Thread.Sleep` in retries. Moving to async `HttpClient` is the bigger rewrite (issue #31).

## Second test round: fixes and additions (2026-11-20) (details; the TEST PLAN above supersedes the order here)

Everything below is on the branch it belongs to (for its PR) **and** merged into `combined-all`. **`combined-all` compiles on GitHub Actions** (Windows + Linux, run 44657053198, commit 194a3bc). None of it has been run yet. Get the latest Windows zip from that run's Artifacts, or `git pull` on `combined-all` and build.

Before testing, kill any running copy: `Get-Process TwitchLeecher | Stop-Process -Force`.

### Sub-only now comes from the Twitch sign-in (added 2026-11-20, in combined-all, build run 44659396373 passed)

`claude/in-app-signin` is now in `combined-all` (f838226). **It is the first test of this branch**, so test it first.
- **Signing in:** happens inside the app (a web view). The login screen's "Also enable sub-only downloads" tick box sets up sub-only in the same login. No console steps.
- **No Sub-only switch anywhere:** it was removed from the header and from Settings (`claude/ui-settings-sections-v2` b06d771). Sub-only is turned on only at sign-in.
- **Subscriber-only VODs:** the message is just **"This video is for subscribers only."**, for the download button, quick download, Download Selected and watched searches (QuickDownloadService, `claude/ui-watched-searches-in-settings` 2fc596a).
- **Token health check:**
  - At start-up and **every hour**, the app asks Twitch (`oauth2/validate`) whether both saved tokens still work.
  - If Twitch **rejects** one (401), it offers: "Your Twitch sign-in has stopped working. Log out now and sign in again?" For the sub-only token: "Sub-only downloads have stopped working. Log out now and sign in again (tick "Also enable sub-only downloads")…".
  - Yes logs out. It asks once per run. Being offline never triggers it.
- **Browser fallback:** the friendly "Signed in to Twitch Leecher" page from the login-page branch is kept for it, alongside the sign-in branch's one-time-code checks (`AuthListener`).

| Test | Needs |
|---|---|
| Sign in with the tick box ticked; sub-only works (download a sub-only VOD from a channel you subscribe to) | Sign-in; the last step needs a subscription |
| Sign in without the tick box, then try a sub-only VOD: "This video is for subscribers only." | Sign-in |
| Disconnect Twitch Leecher at twitch.tv › Settings › Connections, then restart the app: it offers to log out and sign in again | Sign-in (optional) |

### 1. Instant checks (UI only)

| Fix | Branch | What to check |
|---|---|---|
| Search: no ★ by the Channel field | `claude/ui-search-sidebar-93487b` (56835dd) | Only the favourites chips under the field |
| Search: no bottom Search/Cancel | same | The bottom bar is empty on Search; the sidebar button and Enter (in the Channel field) still search |
| Search: Links / IDs box | same | **Root cause:** Enter-to-search was bound to the whole page, so Enter in this box searched instead of adding a line. Now each link goes on its own line, long links wrap, and the box scrolls |
| Header: no Sub-only button | `claude/ui-header-nav-d65209` (596c24d) | Top bar shows tabs, Settings, Info/Donate/Log out only |
| Settings › Search: no favourites list | same | Only a DEFAULT CHANNEL box (Search on start uses it). Favourites are managed with the ★ on results |
| Watched searches row layout | `claude/ui-watched-searches-in-settings` (3397903) | Each row on one line: title, "When found", dropdown, Edit, Active, a red bin. Rows line up, nothing is cut off, even at the minimum window width |
| Watched searches bin | same | The bin asks "Stop watching …?"; Edit no longer has Remove |
| Dialogs centred | `ui-draggable-dialogs` (edf3118) | Move the window to a corner or second monitor, trigger a message box (search a channel that doesn't exist). It opens centred over the window |
| Settings › Download: compression | `feature-compress-video` (3ec4898) | "Compress videos after download" plus a level (Light / Balanced / Strong) under Quick download |

### 2. One short download (3-minute crop)

| Item | Branch | What to check |
|---|---|---|
| Chat as JSON | `feature-chat-html` (1e68853) | Dialog › Extras › Save chat › "as JSON (for other tools)" writes `<video>.chat.json`. The script parses it and checks the messages are in order |
| Compress after download | `feature-compress-video` | Dialog › Extras › "Compress after download" follows Settings. Status goes Downloading → Converting → **Compressing** with progress, and the log says "Compressed X → Y". The file is HEVC (Balanced), plays, and is smaller. Cancelling while compressing leaves no `.compressing` file. **Encoder (added 2c39c53, build run 44711095855 passed):** Settings › Download › Encoder = Auto (default; graphics card if one works, else CPU) / CPU (smallest files, slower) / Graphics card (fastest, files ~20-40% bigger). The log says which encoder ran ("using hevc_nvenc" or "none found, using the CPU"); a failed GPU encode is retried on the CPU. Compare CPU and GPU on the same 3 minutes |
| Dependency updates | `deps-patch-updates` (e1868c6), `deps-remove-unused` (7a0d896) | The app starts, signs in (uses the localhost listener), searches, downloads, opens Settings. Avalonia is 11.3.23, Mvvm 8.4.2, ReactiveUI 11.3.9. SQLite, Annotations, the migration analysers, stale DLL hint paths and the unused ASP.NET framework reference are removed, so the zip should be noticeably smaller |

### 3. Needs a channel that is live now

| Fix | Branch | What to check |
|---|---|---|
| **Watched search downloaded a stream still in progress** (a live channel) | `claude/ui-watched-searches-in-settings` (3397903) | Twitch now gives a recording VOD a normal thumbnail, so the old live check missed it. Broadcast searches now ask Helix `/streams` whether the channel is live, and hold back VODs from the current stream (WAITING … still live). If that check fails, the newest VOD is held back. Test: a watched Broadcasts search for a live channel. Check now must show WAITING and start nothing; after the stream ends, the next check starts it |

### Off-peak download schedule (added 2026-11-20, in combined-all, build run 44709201140 passed)

Branch `feature-download-schedule` (0a030e9). It starts from `claude/ui-watched-searches-in-settings`, because it needs watched searches, per-download pause and the redesigned Settings. Its own handoff: `.claude/handoffs/feature-download-schedule.md`.
- **Settings › Download › OFF-PEAK SCHEDULE:** From/To hours (may cross midnight), "Also start downloads I paused", "Pause them again when the time is up".
- **Outside the window:** a watched-search find is queued paused as **SCHEDULED · WATCHED**, with "Waits for the download schedule".
- **When the window opens:** scheduled items start, plus paused ones if that option is ticked.
- **When it closes:** only what the schedule started pauses again, if that option is ticked.
- **Manual Resume** always starts a download straight away.
- **Test needs:** waiting for a whole hour. Set From to the next hour. Script: `test-feature-download-schedule.ps1`. **Test it last**, after the instant and short-download tests.

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

- **(2026-11-23) Icons:** use `<avalonia:Icon Value="fa-solid fa-name"/>` where `xmlns:avalonia="using:TwitchLeecher.Gui.Controls"`. A new icon needs an entry in `IconData.cs` (view box width and height, SVG path from Font Awesome Free 6). An unknown name draws nothing, and asserts in Debug.
- **(2026-11-23) AOT rules for new code:** no reflection-based `JsonSerializer` (add the type to a `JsonSerializerContext`, or use `JsonObject`); no `Assembly.Location` (use `AppContext.BaseDirectory`); every view needs `x:DataType` (bindings are compiled); register new view models and services in `GuiModule`/`ServiceModule`. CI's start-up check catches crashes at launch, but not a page that only breaks when opened.

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

## Later (as of 2026-11-19; since then: deps updates, Ninject, ReactiveUI, Avalonia 12 and AOT are built, see the top)

- `deps-patch-updates` from `master`: Avalonia 11.3.11 → 11.3.23 (all Avalonia packages), Avalonia.ReactiveUI 11.3.8 → 11.3.9, CommunityToolkit.Mvvm 8.4.0 → 8.4.2.
- `deps-remove-unused` from `master`: System.Data.SQLite (unused, ships native DLLs), System.ComponentModel.Annotations, the Upgrade Assistant analyzers (3 projects).
- Avalonia 12 after the UI PRs settle; replacing Ninject (unmaintained beta); #31 async rewrite (removes obsolete `WebClient`); #42 hover preview; #36 CLI; macOS support; toast notifications (see above).
