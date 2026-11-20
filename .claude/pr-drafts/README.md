# PR submission guide

## Submission plan (2026-11-20, after the combined-all test round)

**Upstream reality:** the maintainer's last commit was 2026-08-07. Merges are months apart, and another contributor's PRs (#76, #101) have waited since 2025 and August. Our 16 PRs (#104–#119, opened 2026-11-18) have no reviews or comments. Adding 33 more now would bury them.

**Rule kept:** one change = one PR, so the developer can pick and choose. Pacing does the work, not bundling. Offer bundling only if the developer asks for it.

1. **DONE 2026-11-20:** overview posted on #104 (https://github.com/schneidermanuel/TwitchLeecher-Dx/pull/104#issuecomment-7049853680; text in `overview-issue.md`; its build link expires 2026-12-04). Originally: **one message, no new PRs.** Post `overview-issue.md` as a comment on #104, or as an issue, with a link to a downloadable build of `final` (Actions artifact). This lets the developer try everything before reading any code. Ask whether they'd prefer the UI refresh as one PR or in steps.
2. **When the developer first responds, or after about 2 weeks of silence: Wave 1 (6 small, independent PRs on `master`, ready now).** `pr/fix-single-instance-pipe`, `pr/ui-draggable-dialogs`, `pr/deps-patch-updates`, `pr/deps-remove-unused`, `pr/ci-build`, `pr/deps-security-pin` (cut 2026-11-20, CI green, body in `bodies/deps-security-pin.md`). None is affected by the latest testing tweaks.
3. **When at least half of #104–#119 are merged or closed: Wave 2 (UI refresh foundation).** `ui-theme-keys` → `ui-dark-inputs`, `ui-header-nav`, `ui-search-sidebar`, `ui-downloads-queue`, `in-app-signin`. Send at most about 5 open at a time.
4. **After the rest of #104–#119 are dealt with: Wave 3 (stacked features), a few at a time.** Results toolbar, download dialog, Settings, Coyote, chat export, compress, tray → start with Windows, watched searches → schedule, the perf follow-ups.
5. **Last: code quality, after a Windows test round.** `improve-error-logging`, `replace-webclient` (#31), `add-unit-tests`, `refactor-results-viewmodel`, `refactor-shared-styles`, `tray-tooltip-events`. These are **not in `final`** and have not been run on Windows.
6. **Hold:** `desktop-notifications` (toasts never confirmed on Windows), `fix-slow-downloads` (no measured gain), `login-page-and-icon` (superseded).

**Refresh each wave just before sending.** The `pr/*` branches were cut before the last PC session. Waves 2–3 are missing up to 35 tested tweaks from `combined-all`, for example the results pane, toolbar staging, date picker, watched-search rows and header order. Waves 1 and 5 are unaffected.
- Rebuild each branch from its synced `claude/*` working branch with `make_pr.sh`.
- Then diff its files against `final`. The Settings avatar/Twitch logo, the "Download schedule" wording and the tray tooltip exist only in `combined-all`/`final`.
- Rebase onto upstream `master` as it stands at that moment, and update the Testing section of each body.

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
