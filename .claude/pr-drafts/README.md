# PR submission guide

## Check 2026-11-23 (evening): issues, frameworks, optimisations

**Upstream:** master has not moved (24cab52), and all 13 open PRs can merge. The developer added a thank-you on #104 (16:25, before the email): since moving to Avalonia he has only kept the app working and fixed critical bugs. No reply yet on bigger versus small PRs.

**Open issues (16):**
- **12 have a fix in a PR:**
  - Open: #103/#92/#87/#56 -> #104; #48 -> #105; #37 -> #106; #71 -> #107 (+ chat export, Wave 3); #96 -> #108; #49 -> #109; #85 -> #127.
  - Prepared: #36 -> `pr/feature-cli`.
  - (#35, #50, #86 were already closed by the merges.)
- **#42** (hover preview): set aside by the user; the download dialog filmstrip (Wave 3) covers it.
- **#31** (async HttpClient): **not done.** `replace-webclient` (2dfa767, cut on the old master, never run) swaps WebClient for a shared HttpClient. final still has 28 WebClient uses in ApiService, DownloadService and UpdateService.
- **#102** (only "Audio only" offered): that VOD has been deleted, so it can't be reproduced.
- **#38** (downloads not starting, v3.6.1 from 2024) and **#25** (download button, Windows 7): neither reproduces. .NET 10 does not run on Windows 7, so #25 can't be fixed; its light-mode part was fixed by the developer in 2024.

**Frameworks:**
- .NET 10 is LTS (patch 10.0.15). .NET 11 shipped 2026-11-10 but is STS, so stay on 10.
- Avalonia **12.2.0 came out 2026-11-23**: wait about two weeks (about 2026-12-07), then a patch-update PR.
- Microsoft.Extensions.DependencyInjection and ProtectedData 11.0.0 are .NET 11 releases; keep 10.0.x to match the LTS.
- **Newtonsoft.Json is pinned to 13.0.5-beta1 (a pre-release) in upstream master**; 13.0.4 is the latest stable. Small PR: pin 13.0.4. Bigger option: replace Newtonsoft with System.Text.Json (50 uses in 2 files), which removes a dependency and most of the AOT warnings.
- Everything else is current (CommunityToolkit.Mvvm 8.4.2, DataGrid/WebView 12.1.2, ProgressRing 0.11.0, DesktopNotifications 1.3.1, the CI actions).

**Worth doing next (ask the user):** 1) #31 WebClient -> HttpClient, rebuilt on the new master, in final, PC-tested; 2) Newtonsoft stable pin; 3) Avalonia 12.2 after about 2026-12-07.

## Release notes draft (2026-11-23)

`release-notes-4.0.md` is a working draft of the 4.0 release notes in the developer's own template. **Whenever a PR merges or is declined, update it:** tick or remove its line using the map at the bottom. If he releases the merged PRs first (for example as 3.10), move those lines out. Offer it to him only when the last wave is in, and only with the user's OK.

## "What's new" card (built 2026-11-23, in `final`, not yet a PR branch)

A one-time card on the first run of a new feature release after an upgrade. The design and wording were approved by the user (mockup Option A). Seen on the PC 2026-11-23: looks right. It goes **last**, after the other PRs: its eight items must match what actually merged, and the version number is the developer's call. When cutting `pr/whats-new` from `final`, leave out the 4.0.0 version bump (`GlobalAssemblyInfo.cs`, `TwitchLeecher.csproj` FileVersion), so it shows once he sets the version. Body to write then.

## Email sent to the developer, 2026-11-23

The user sent `email-developer.md`: the same update and offer as the #104 comment, including grouping the ~26 remaining PRs into about 6. Wait for his answer before cutting or sending anything.

## Branches updated to the new master, 2026-11-23

- Fork `master` = upstream `master` (24cab52, with #110-#119).
- `final`: master merged in, keeping final's tree, because the 10 merged commits are byte-for-byte our PRs. Also gained the #107 `JObject` chat request and the #109 "Keep the unconverted video if conversion fails" setting. CI green.
- `preview`: rebuilt as one commit on the new master from `final`.
- **Every `pr/*` branch now sits on the new master:**
  - Wave 2 and the code-quality set (13 branches): rebased, each CI-green on Windows and Linux.
  - `ui-downloads-queue` needed real resolution against #111, #113 and #115. It keeps the queue's pause clean-up and failure stages inside #111's streaming structure, and the view's ScrollViewer is now `x:Name="Scroller"` for #113's `ScrollPositionRestorer` instead of the old `Offset` binding. `perf-downloads-idle` is its last commit on top.
  - `pr/_after-open-prs`, `pr/_after-open-prs-theme` and the 13 Wave 3 branches were re-parented onto the new master with **identical file contents**, so no rebuild was needed. The base still holds the six open PRs (#104-#109) as they were before today's review changes. Re-cut it when those merge.
- **Heads-up:** #104-#109 conflict with each other (for example #106 and #107 both touch `ApiService`/`DownloadService`), so each merge will need the next one rebased.

## Wave 1 SENT 2026-11-23 (user: open it now that the developer is responding)

Rebased onto the new master (24cab52) and each one CI-green on Windows and Linux, separately and all seven stacked. The three dependency PRs apply in any order: the Tmds.DBus.Protocol pin now sits in its own ItemGroup above the PropertyGroups, so it no longer touches the lines the other two change.

| PR | Branch |
|---|---|
| #121 | `pr/fix-single-instance-pipe` |
| #122 | `pr/ui-draggable-dialogs` |
| #123 | `pr/deps-patch-updates` |
| #124 | `pr/deps-remove-unused` |
| #125 | `pr/ci-build` |
| #126 | `pr/deps-security-pin` |
| #127 | `pr/feature-85-open-folder` (closes #85) |

New issue #120 (crop time boxes cut off) was closed by its reporter as a duplicate of #103. #104 already fixes it (time boxes widened 50 to 80, format "00"). Posted on #120 and added to #104's description (2026-11-23).

**Next:** Wave 2 (UI refresh foundation) once the open PRs #104-#109 and #121-#127 are mostly dealt with. Rebuild `final` and the Wave 2/3 branches on the new master first, including the #107 JObject and #109 setting changes.


- **Merged:** #110-#119 (10 of 16). The developer is happy to merge despite style differences and plans a rewrite later (#119). On #110 they may later move ffmpeg into the installer only.
- **Asked and done** (rebased onto the new master, CI green on Windows and Linux, replies posted):
  - #105, #106, #104: rebase only.
  - #107: the chat GQL request is now a `JObject`, with `CHAT_OPERATION`/`CHAT_QUERY_HASH` constants.
  - #108 (approved, "LGTM"): rebased. Its own scroll-restore code was dropped because #113's `ScrollPositionRestorer` covers it. The title is now "Quick download and multi-select download (#96)".
  - #109: new setting `DownloadKeepTsOnFailure`, Settings > Download > "Keep the unconverted .ts if conversion fails". It's on by default (XML `KeepTsOnFailure`). Since #111, the streamed path re-joins the chunks into the `.ts`. The setting itself hasn't been run on Windows yet.
- **Not yet in `final`:** the #107 `JObject` change and the #109 setting. Port them when `final` is next rebuilt on the new master.
- **Wave conditions now met:** the developer has responded (Wave 1, sent, see above), and at least half of #104-#119 are merged (Wave 2).


- **`final-next` passed the Windows test round** (checks 1-13 in the handoff), with these fixes made on the PC and now in the PR branches:
  - Avalonia 12 start-up crash (tray icon asset read before the Gui assembly loaded): `pr/avalonia-12`.
  - CLI: search dates set, no-op desktop notifications: `pr/feature-cli` (the notification part is only in `final`; master has no desktop notifications).
  - Compress: NVENC quality scale, CPU retry when the GPU result isn't smaller: `pr/compress-video`.
  - Downloads: faster Cancel with a "Cancelling" message, no text on queued cards: `pr/ui-downloads-queue`.
  - Search sidebar FAVOURITES header: `pr/ui-search-sidebar`. Only in `final` for now: results toolbar with no animation on entering Search (fold into `pr/ui-results-toolbar` at the Wave 3 refresh) and the frameless message box (needs the theme colours and Avalonia 12's `WindowDecorations`, so it is a small follow-up after `pr/avalonia-12`, not part of Wave 1's `pr/ui-draggable-dialogs`).
- **Three PC tweaks from 2026-11-20 that only lived on `preview` are now in `final`** (user, 2026-11-23: keep all three): Default quality on one line (folded into `pr/ui-settings-sections`), corner pop-ups removed, and a message box when a quick download queues nothing. The last two touch about 15 PR branches, so they are **one follow-up PR sent last**: body `bodies/remove-popups.md`, no branch yet (cut it from `final` once the others merge).
- **Desktop notifications are not a requirement** (user): `pr/desktop-notifications` stays on hold.
- **`final` = `final-next`**, `preview` rebuilt from it, and the #104 comment updated with the new build link and the code-quality and issue PRs.
- Testing sections of the bodies are updated for everything checked in this round.
- **#104 comment updated** (2026-11-23): new preview build, run 45237723230 since the master update (artifacts until 2026-12-07; Windows is the native AOT build), plus #85, the code-quality PRs, the CLI (#36) and fewer pop-ups.
- **Wave 1 waits until 2026-12-04** (user, 2026-11-23), unless the developer replies first. All 7 are ready and passed the AI-reference check today.
- **Branches tidied** (2026-11-23). The fork keeps `master`, `final`, `preview`, the `pr/*` branches, the 16 `*-pr` branches behind #104-#119, and the unsubmitted work not in `final` (`add-unit-tests`, `improve-error-logging`, `replace-webclient`, `refactor-results-viewmodel`, `claude/refactor-shared-styles-35c0a9`, `tray-tooltip-events`, `claude/fix-slow-downloads-15ccca`, `deps-security-notifications`). The other 41 working branches (including `combined`, `combined-all`, `final-next` and the `claude/*` UI branches) moved to hidden refs `refs/archive/<name>`. Get them back with `git fetch origin 'refs/archive/*:refs/archive/*'`, then `git branch <name> refs/archive/<name>`.

## Submission plan (2026-11-20, after the combined-all test round)

**Upstream reality:** the maintainer's last commit was 2026-08-07. Merges are months apart, and another contributor's PRs (#76, #101) have waited since 2025 and August. Our 16 PRs (#104–#119, opened 2026-11-18) have no reviews or comments. Adding 33 more now would bury them.

**Rule kept:** one change = one PR, so the developer can pick and choose. Pacing does the work, not bundling. Offer bundling only if the developer asks for it.

1. **DONE 2026-11-20:** overview posted on #104 (https://github.com/schneidermanuel/TwitchLeecher-Dx/pull/104#issuecomment-7049853680; text in `overview-issue.md`; it links the `preview` branch and that branch's build, run 44813082025, which expires 2026-12-04). Originally: **one message, no new PRs.** Post `overview-issue.md` as a comment on #104, or as an issue, with a link to a downloadable build of `final` (Actions artifact). This lets the developer try everything before reading any code. Ask whether they'd prefer the UI refresh as one PR or in steps.
2. **When the developer first responds, or after about 2 weeks of silence: Wave 1 (6 small, independent PRs on `master`, ready now).** `pr/fix-single-instance-pipe`, `pr/ui-draggable-dialogs`, `pr/deps-patch-updates`, `pr/deps-remove-unused`, `pr/ci-build`, `pr/deps-security-pin` (cut 2026-11-20, CI green, body in `bodies/deps-security-pin.md`), `pr/feature-85-open-folder` (issue #85, cut 2026-11-23, body in `bodies/feature-85-open-folder.md`). None is affected by the latest testing tweaks.
3. **When at least half of #104–#119 are merged or closed: Wave 2 (UI refresh foundation).** `ui-theme-keys` → `ui-dark-inputs`, `ui-header-nav`, `ui-search-sidebar`, `ui-downloads-queue`, `in-app-signin`. Send at most about 5 open at a time.
4. **After the rest of #104–#119 are dealt with: Wave 3 (stacked features), a few at a time.** Results toolbar, download dialog, Settings, Coyote, chat export, compress, tray → start with Windows, watched searches → schedule, the perf follow-ups.
5. **Last: code quality (passed the 2026-11-23 Windows round).** Also `pr/feature-cli` (issue #36, the `tl` command-line tool, stacked on replace-ninject, body `bodies/feature-cli.md`). Ready on the fork: `pr/remove-reactiveui` and `pr/replace-icon-pack` (both on `master`, independent), then `pr/native-aot` (stacked on replace-ninject + remove-reactiveui) and `pr/avalonia-12` (stacked on remove-reactiveui + replace-icon-pack; send it after the UI PRs, since it touches every project file). Bodies are in `bodies/`. All four are in `final-next` for the 2026-11-23 test round. `pr/replace-ninject` (on `master`, CI green, body `bodies/replace-ninject.md`; also applied to `final`/`preview` for testing; it unlocks Native AOT), `improve-error-logging`, `replace-webclient` (#31), `add-unit-tests`, `refactor-results-viewmodel`, `refactor-shared-styles`, `tray-tooltip-events`. These are **not in `final`** and have not been run on Windows.
6. **Hold:** `desktop-notifications` (toasts never confirmed on Windows), `fix-slow-downloads` (no measured gain), `login-page-and-icon` (superseded).

**Refresh each wave just before sending.** The `pr/*` branches were cut before the last PC session. Waves 2–3 are missing up to 35 tested tweaks from `combined-all`, for example the results pane, toolbar staging, date picker, watched-search rows and header order. Waves 1 and 5 are unaffected.
- Rebuild each branch from `final` (the tested code; the old `claude/*` working branches are in `refs/archive/`, and some are behind `final`).
- Diff its files against `final`. The Settings avatar/Twitch logo, the "Download schedule" wording and the tray tooltip exist only in `combined-all`/`final`.
- Rebase onto upstream `master` as it stands at that moment, and update the Testing section of each body.

**`preview` branch (public face):** one clean commit on `master` with `final`'s code, minus `.claude/` and `test-scripts/`, with no AI lines. The #104 comment links it. When `final` changes, rebuild it: start a worktree on `origin/master`, run `git read-tree -u --reset origin/final`, `git rm -r --cached` the notes and scripts, commit, then `git push --force-with-lease origin preview`.

**`final` branch:** `combined-all` plus this guide (from `combined`), the tested version to use while waiting. `combined` and `combined-all` are kept, but `final` supersedes both.

---

## Earlier detail (prepared before the last test round, nothing submitted)

All 25 changes not yet sent upstream are now **clean PR branches** named `pr/*` (pushed to the fork). Each one is:
- **one squashed commit** with a plain message, on the right base;
- **free of tooling:** no test scripts, no `.claude/` notes, no AI attribution;
- **compiled on GitHub Actions** on its own (see "Build check" below).

**Nothing has been submitted.** Upstream (`schneidermanuel/TwitchLeecher-Dx`) still has the 16 open PRs #104–#119, with no reviews or comments yet.

## Making the developer's life easier

1. **Don't add 25 PRs on top of 16 unreviewed ones in one go.** Send them in the waves below, starting once the developer starts responding. A maintainer facing 41 open PRs from one person is likely to stall.
2. **Post one overview first.** The ready-made text is in `overview-issue.md`, for an issue or a comment on #104. It lists every PR, what it does, and the order and dependencies, so the developer can pick and choose without reading 41 descriptions.
3. **Stacked PRs go up as drafts.** Where a PR builds on another unmerged one, its diff includes that one until it merges. Open those as **draft** PRs, say "depends on #…, please review only the last commit", and rebase once the dependency lands.
4. **Two branches are deliberately not prepared:**
   - **slow-downloads:** the Windows test showed no speed gain (8.74 vs 8.31 MB/s), and it conflicts with the per-download pause.
   - **login-page-and-icon:** superseded by the in-app sign-in. Its icon part is already in #104.
5. **The 16 open PRs could also be tidied, but only if the developer asks.** For example, the three small performance PRs (#114, #115, #116) could be offered as one. They're already open, so don't churn them unprompted.
6. **Rebasing needed later:** `pr/ui-downloads-queue` (built on `master`) will conflict with #104, #111, #113 and #115 in `DownloadService.cs`, `DownloadsView.axaml` and `TwitchVideoDownload.cs`. A version already resolved against those PRs exists inside `pr/watched-searches`; use it when rebasing.

## Waves

**Status:**
- **T** = tested on Windows in an earlier round.
- **T\*** = tested, with later changes not yet re-tested.
- **–** = not tested yet.

### Wave 1: small, independent, based on `master` (send any time)

| Branch | PR title | Size | Status |
|---|---|---|---|
| `pr/fix-single-instance-pipe` | Fix second launch not bringing the running window to the front | 3 files | – |
| `pr/ui-draggable-dialogs` | Make message boxes draggable and open them centred on the main window | 3 files | T\* (centring is new) |
| `pr/deps-patch-updates` | Update Avalonia to 11.3.23 and other patch releases | 5 files | – |
| `pr/deps-remove-unused` | Remove unused package and framework references | 4 files | – |
| `pr/ci-build` | Add a GitHub Actions build | 1 file | builds pass on the fork |

### Wave 2: UI refresh foundation, based on `master`

| Branch | PR title | Depends on | Status |
|---|---|---|---|
| `pr/ui-theme-keys` | Add named theme colours for the UI refresh | — | T |
| `pr/ui-dark-inputs` | Dark form controls and a consistent font scale | theme colours | T\* (date picker centring + calendar new) |
| `pr/ui-header-nav` | Redesigned header and corner notifications | theme colours | T\* (Sub-only and Log out moved, logo → Search) |
| `pr/ui-search-sidebar` | Search page as a sidebar, opening on Search | theme colours | T\* (round-2 fixes, date limits new) |
| `pr/ui-downloads-queue` | Downloads page: speed, time left and per-download pause | theme colours (rebase after #104/#111/#113/#115) | T |
| `pr/in-app-signin` | Sign in to Twitch inside the app, with sub-only in the same login | theme colours | – |

### Wave 3: needs the open PRs #104–#119 merged first

These sit on `pr/_after-open-prs`: a single commit on `master` holding the code of all 16 open PRs (the same code as `combined`, without its history or working notes), plus `pr/_after-open-prs-theme`, which adds the theme colours. When it's time: `git rebase --onto upstream/master pr/_after-open-prs-theme pr/<name>` (or `pr/_after-open-prs` for the ones without theme colours).

| Branch | PR title | Builds on | Status |
|---|---|---|---|
| `pr/ui-results-toolbar` | Search results: toolbar, badges, channel header and selection bar | #106, #108, theme colours | T\* (channel ★ new) |
| `pr/ui-download-dialog` | Download dialog: crop preview filmstrip and tidier layout | #106, #107, #117, #119, theme colours | T |
| `pr/ui-settings-sections` | Settings with a side menu, filename chips and a save bar | #108, #118, theme colours | T\* (account + Theme in General, no Advanced: new) |
| `pr/theme-coyote` | Add a "Coyote" colour theme | theme colours | T |
| `pr/chat-export` | Chat export as HTML (with emotes) or JSON | #107, #117 | – |
| `pr/compress-video` | Option to compress videos after download | open PRs | – |
| `pr/tray` | System tray icon and minimise to tray | open PRs | T\* (first-minimise prompt new) |
| `pr/start-with-windows` | Option to start with Windows, minimised to the tray | tray, pipe fix | – (label now "when Windows starts") |
| `pr/desktop-notifications` | Desktop notifications when downloads finish | tray | – (toasts never showed in round 1) |
| `pr/watched-searches` | Watched searches: automatically download new VODs and clips | Settings, Downloads page, #106, #108 | T\* (live-stream fix, layout, On/Off switch new) |
| `pr/download-schedule` | Off-peak download schedule | watched searches | – |
| `pr/perf-downloads-idle` | Downloads page: only refresh every second while something downloads | Downloads page PR | – |
| `pr/perf-result-badges` | Search results: cheaper badge refresh, and DOWNLOADED when a download finishes | results toolbar PR | – |
| `pr/perf-parallel-chat` | Chat export: fetch long ranges in parallel | chat export PR | – |

**Recommendation:** hold back `pr/desktop-notifications` and `pr/download-schedule` until they've passed a Windows test. Submit the rest of a wave once `combined-all` has passed the current test round.

## How to submit one (when ready)

```
git push origin pr/<name>          # already pushed
gh pr create --repo schneidermanuel/TwitchLeecher-Dx --base master --head bluefroguk1:pr/<name> --draft \
  --title "<title from the table>" --body-file .claude/pr-drafts/bodies/<name>.md
```

Remove `--draft` for Wave 1 and for any PR whose dependencies have merged. Bodies are in `bodies/`. Each body's Testing section only claims what was confirmed on Windows; update it after the next test round.

## Build check

Each `pr/*` branch was pushed as `ci-check/<name>` with the CI workflow added, and built on GitHub Actions (Windows + Linux). **All 22 built successfully** on 2026-11-21 (runs 44719692910–44719828045), and the 3 optimisation branches after them (runs 44721593073–44721602281). The temporary `ci-check/*` branches were deleted afterwards.

**Round 3 UI tweaks (2026-11-20):** `pr/ui-header-nav`, `pr/ui-dark-inputs`, `pr/ui-search-sidebar`, `pr/start-with-windows`, `pr/ui-settings-sections`, `pr/watched-searches` and `pr/download-schedule` were rebuilt as single commits (same messages) and all **7 built successfully** (runs 44752599273–44752633098; download-schedule's first attempt hit an ffmpeg mirror 503 and passed on re-run). No Claude references (check re-run: clean).

After that, the stacked branches were re-based onto a **clean one-commit base on `master`** (`pr/_after-open-prs`), with identical code. Previously their history included `combined`'s working commits, which carry AI co-author lines. A full check from `master` (commit messages, author and committer details, file paths and added lines) now finds **no reference to Claude, AI tools or co-authors in any `pr/*` branch**. The 16 open PRs and their descriptions are clean too.

**Before submitting anything, re-run that check:**

```
for b in $(git for-each-ref --format='%(refname:short)' 'refs/heads/pr/*'); do git log --format='%an %ae %cn %ce%n%B' origin/master..$b; git diff origin/master $b; done | grep -iE 'claude|anthropic|co-authored|generated with'
```

It must print nothing.
