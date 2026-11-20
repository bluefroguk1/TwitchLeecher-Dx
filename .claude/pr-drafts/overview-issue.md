<!-- For an issue titled "Overview of my pull requests (order and dependencies)", or a comment on #104. -->

Hi! I've sent a number of pull requests and have more ready. To make them easier to review, here's an overview of everything, which parts depend on which, and the order that avoids conflicts. Each PR is a single commit and can be accepted or declined on its own; just let me know if you'd rather I combine, split or drop any of them.

**Already open (#104–#119)**
- Fixes and performance: #104 (resource usage, #56, #87, #92, #103), #110 (ffmpeg 9.0.2 fetched at build time), #111 (stream chunks into ffmpeg), #112 (stay on search after an error), #113 (keep scroll position), #114/#115/#116 (small performance fixes)
- Features: #105 (HEVC/AV1, #48), #106 (clips, #37), #107 (save chat, #71), #108 (quick download / multi-select, #96), #109 (keep the .ts if conversion fails, #49), #117 (VOD info, #50), #118 (Favourites as default search, #86), #119 (estimated size, #35)

**Ready next, independent and small**
- Second launch brings the running window to the front (pipe name mismatch fix)
- Draggable message boxes, opened centred on the main window
- Avalonia 11.3.23 and other patch updates
- Remove unused packages (SQLite, ASP.NET framework reference, ...) for a smaller build
- A GitHub Actions build (Windows + Linux) with downloadable artifacts

**A UI refresh, in small steps** (all use one new set of named theme colours, so every theme keeps working)
1. Named theme colours (the base for the rest)
2. Dark form controls and a consistent font scale
3. Header with Search/Downloads tabs, and corner notifications
4. Search page as a sidebar, opening on Search
5. Downloads page with speed, time left and per-download pause
6. Twitch sign-in inside the app, with sub-only in the same login (no more browser console step)

**After the open PRs above** (they build on them)
- Search results toolbar (sort, filter, badges, channel favourite star) - needs #106, #108
- Download dialog with a crop preview filmstrip - needs #106, #107, #117, #119
- Settings with a side menu and a save bar - needs #108, #118
- A "Coyote" colour theme
- Chat export as HTML with emotes, or JSON - needs #107
- Compress videos after download (CPU or GPU)
- Tray icon / minimise to tray, start with Windows, desktop notifications
- Watched searches (automatically download new VODs/clips from saved searches) and an off-peak download schedule

I'll send the next ones in small batches as you get to them, and I'm happy to rebase anything if master moves.
