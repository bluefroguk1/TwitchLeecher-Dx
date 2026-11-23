Hi! I've sent a number of pull requests and have more ready, so here's an overview of everything: what each part does, what depends on what, and an order that avoids conflicts. There's no rush on any of it. Each PR is a single commit and can be accepted or declined on its own. Let me know if you'd rather I combine, split or drop any of them.

**See it all in one place:** the [`preview` branch](https://github.com/bluefroguk1/TwitchLeecher-Dx/tree/preview) on my fork is a single commit on master with every change below merged in ([full diff](https://github.com/schneidermanuel/TwitchLeecher-Dx/compare/master...bluefroguk1:TwitchLeecher-Dx:preview)). It's only for looking at, not for merging. Ready-made Windows and Linux builds of it are on [this Actions run](https://github.com/bluefroguk1/TwitchLeecher-Dx/actions/runs/45222297875) (Artifacts, GitHub login needed, available until 7 December; the Windows one is a native AOT build), so you can try the result before reading any code.

**Merged, thank you!** #110–#119.

**Open now** (all rebased on the current master)
- Fixes: #104 (resource usage, #56, #87, #92, #103 and its duplicate #120)
- Features: #105 (HEVC/AV1, #48), #106 (clips, #37), #107 (save chat, #71, review change done), #108 (quick download / multi-select, #96), #109 (keep the .ts if conversion fails, #49, now with a setting to turn it off)
- Small and independent: #121 (second launch brings the running window to the front), #122 (draggable message boxes), #123 (Avalonia 11.3.23 and patch updates), #124 (remove unused packages), #125 (GitHub Actions build), #126 (security pin for Tmds.DBus.Protocol, Linux), #127 (Open folder for every download, #85)

#104–#109 overlap in a few files, so once one is merged I'll rebase the others. #123, #124 and #126 merge in any order.

**Next: a UI refresh** (built on one new set of named theme colours, so every theme keeps working)
1. Named theme colours (the base for the rest)
2. Dark form controls and a consistent font scale
3. Header with Search/Downloads tabs, and corner notifications
4. Search page as a sidebar with results alongside, opening on Search
5. Downloads page with speed, time left and per-download pause
6. Twitch sign-in inside the app, with sub-only in the same login (no more browser console step)

Would you prefer the UI refresh as one PR, or in the steps above?

**After that** (these build on the open PRs)
- Search results toolbar (sort, filter, badges, channel favourite star): needs #106, #108
- Download dialog with a crop preview filmstrip: needs #106, #107
- Settings with a side menu and a save bar: needs #108
- A "Coyote" colour theme
- Chat export as HTML with emotes, or JSON: needs #107
- Compress videos after download (CPU or GPU)
- Tray icon / minimise to tray, and start with Windows
- Watched searches (automatically download new VODs/clips from saved searches) and a download schedule

**Later, code quality** (all tested on Windows, each its own PR)
- Microsoft.Extensions.DependencyInjection in place of Ninject
- Remove ReactiveUI (three commands move to the existing `DelegateCommand`)
- A small built-in icon control in place of the icon package (same Font Awesome shapes)
- Native AOT for the Windows build: faster start-up and less memory (Linux stays as it is)
- Avalonia 12
- A command-line tool, `tl search` / `tl qualities` / `tl download`, using the app's settings and sign-in (#36)
- Fewer pop-ups: the corner pop-ups that repeat what the page already shows are dropped

**Later, tidy-ups:** logging of swallowed exceptions, `HttpClient` in place of the obsolete `WebClient` (#31), and a small unit test project.

I won't send anything new until most of the open ones are dealt with, then the next batch in small steps. Happy to rebase anything if master moves.
