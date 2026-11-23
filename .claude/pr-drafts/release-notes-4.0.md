<!--
Working draft of the 4.0 release notes, in the developer's own release template (see v3.9.1, v3.9.0, v3.8.1).
Keep it in step with what merges:
- Each line maps to one of our PRs (see the map at the bottom). When a PR is declined, take its line out.
- When a PR merges, tick it in the map.
- If he releases the merged ones first (e.g. as 3.10), move those lines out of this draft.
Offer it to him (PR comment or email) only when the last wave is in. [commit] and [date] are his to fill in.
Nothing below this comment block is sent as-is without the user's OK.
-->
## TwitchLeecher-DX Release Version 4.0.0
> Last commit: [commit]
> Built: [date] by @schneidermanuel on Arch Linux

## What's Changed
**A fresh new look**
* Redesigned header, search sidebar, Downloads page and Settings, in all three themes plus the new "Coyote" theme
* Sign in to Twitch inside the app, with sub-only VODs in the same login
* A one-time "What's new" card for people upgrading

**New features**
* Watched searches: new videos and clips download automatically, with an optional download schedule
* Start with Windows, minimise to the tray
* Search and download clips (#37)
* Save chat as a web page with emotes, or JSON (#71), and VOD details as JSON (#50)
* Compress videos after download, using the graphics card when available
* Quick download and multi-select download (#96)
* Favourites as the default search (#86), plus a favourite star on each channel
* HEVC/AV1 qualities (#48), and an estimated download size (#35)
* A command-line tool, `tl` (#36)

**Downloads**
* Faster downloads, with chunks streamed straight into ffmpeg
* Speed, time left and pause for each download
* Keep the unconverted .ts if conversion fails, which can be turned off (#49)
* Open folder for every download (#85)
* With conversion off, newer VODs can be saved as .mp4 (they are MP4 inside)

**Fixes**
* Lower CPU and memory use (#56), and several reported bugs (#87, #92, #103, #120)
* A second launch brings the running window to the front
* "Check for updates" works again (it had been asking for a repository that doesn't exist since 2023)
* Stay on the search page after a failed search
* Keep the scroll position on Downloads and Settings
* Draggable message boxes, opened centred on the main window

**Under the hood**
* Native AOT build for Windows: faster start-up, no .NET runtime to install
* Avalonia 12; Microsoft DI in place of Ninject; ReactiveUI and the icon package removed
* ffmpeg 9.0.2, downloaded at build time
* GitHub Actions builds for Windows and Linux
* One shared HttpClient in place of the obsolete WebClient (#31)
* .NET's built-in JSON reader in place of Newtonsoft.Json
* Patched Tmds.DBus.Protocol (security advisory GHSA-xrw6-gwf8-vvr9, Linux)

### Issues closed with this release:
* #35
* #36
* #37
* #48
* #49
* #50
* #56
* #71
* #85
* #86
* #87
* #92
* #96
* #31
* #103
* #120

### Contributers
* @schneidermanuel
* @bluefroguk1

**Full Changelog**: https://github.com/schneidermanuel/TwitchLeecher-Dx/compare/v3.9.1...v4.0.0

<!--
Map: line -> PR (x = merged upstream)
[x] ffmpeg 9.0.2 -> #110 | [x] streamed into ffmpeg -> #111 | [x] stay on search -> #112 | [x] scroll position -> #113
[x] perf (rate limit, log, thumbnails) -> #114 #115 #116 | [x] VOD info -> #117 | [x] favourites default -> #118 | [x] size estimate -> #119
[ ] fixes, #56 #87 #92 #103 #120 -> #104 | [ ] HEVC/AV1 -> #105 | [ ] clips -> #106 | [ ] chat (text) -> #107 | [ ] quick/multi-select -> #108 | [ ] keep .ts -> #109
[ ] second launch -> #121 | [ ] draggable dialogs -> #122 | [ ] Avalonia 11.3.23 -> #123 | [ ] unused packages -> #124 | [ ] GitHub Actions -> #125 | [ ] DBus pin -> #126 | [ ] Open folder -> #127
[ ] UI refresh: theme colours, dark inputs, header, search sidebar, downloads queue, in-app sign-in -> Wave 2
[ ] results toolbar (favourite star), download dialog, settings, Coyote, chat export (HTML/JSON), compress, tray, start with Windows, watched searches, schedule, perf follow-ups -> Wave 3
[ ] Ninject, ReactiveUI, icons, Native AOT, Avalonia 12, tl CLI -> last wave | [ ] fewer pop-ups -> remove-popups | [ ] What's new card -> whats-new | [ ] HttpClient (#31) -> replace-webclient | [ ] System.Text.Json -> system-text-json | [ ] .mp4 when unconverted -> fix-mp4-unconverted | [ ] update check -> #128
-->
