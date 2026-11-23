Hi! I've sent a number of pull requests and have more ready, so here's an overview of everything: what each part does, what depends on what, and an order that avoids conflicts. There's no rush on any of it. Each PR is a single commit and can be accepted or declined on its own. Let me know if you'd rather I combine, split or drop any of them.

**See it all in one place:** the [`preview` branch](https://github.com/bluefroguk1/TwitchLeecher-Dx/tree/preview) on my fork is a single commit on master with every change below merged in ([full diff](https://github.com/schneidermanuel/TwitchLeecher-Dx/compare/master...bluefroguk1:TwitchLeecher-Dx:preview)). It's only for looking at, not for merging. Ready-made Windows and Linux builds of it are on [this Actions run](https://github.com/bluefroguk1/TwitchLeecher-Dx/actions/runs/45195138459) (Artifacts, GitHub login needed, available until 7 December; the Windows one is a native AOT build), so you can try the result before reading any code.

**Already open (#104–#119)**
- Fixes and performance: #104 (resource usage, #56, #87, #92, #103), #110 (ffmpeg 9.0.2 fetched at build time), #111 (stream chunks into ffmpeg), #112 (stay on search after an error), #113 (keep scroll position), #114/#115/#116 (small performance fixes)
- Features: #105 (HEVC/AV1, #48), #106 (clips, #37), #107 (save chat, #71), #108 (quick download / multi-select, #96), #109 (keep the .ts if conversion fails, #49), #117 (VOD info, #50), #118 (Favourites as default search, #86), #119 (estimated size, #35)

**Ready next, small and independent**
- Second launch brings the running window to the front (pipe name mismatch fix)
- Draggable message boxes, opened centred on the main window
- Avalonia 11.3.23 and other patch updates
- Pin the patched Tmds.DBus.Protocol (security advisory GHSA-xrw6-gwf8-vvr9, Linux)
- Remove unused packages (SQLite, ASP.NET framework reference, ...) for a smaller build
- A GitHub Actions build (Windows + Linux) with downloadable artifacts
- Open folder for every download, not only finished ones (#85)

**A UI refresh** (built on one new set of named theme colours, so every theme keeps working)
1. Named theme colours (the base for the rest)
2. Dark form controls and a consistent font scale
3. Header with Search/Downloads tabs, and corner notifications
4. Search page as a sidebar with results alongside, opening on Search
5. Downloads page with speed, time left and per-download pause
6. Twitch sign-in inside the app, with sub-only in the same login (no more browser console step)

Would you prefer the UI refresh as one PR, or in the steps above?

**After the open PRs above** (they build on them)
- Search results toolbar (sort, filter, badges, channel favourite star): needs #106, #108
- Download dialog with a crop preview filmstrip: needs #106, #107, #117, #119
- Settings with a side menu and a save bar: needs #108, #118
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

I'll send these in small batches as you get to the open ones, and I'm happy to rebase anything if master moves.
