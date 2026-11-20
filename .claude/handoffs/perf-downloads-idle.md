# Handoff: perf-downloads-idle

The Downloads page's one-second refresh timer ran forever, even with nothing downloading. It now runs only while a download is downloading or queued. When nothing is active it stops, and the page refreshes once whenever a download is added, removed or changes state (started, paused, finished, failed).

**Test needs:** instant plus one short download.
1. With nothing downloading, Task Manager shows Twitch Leecher at 0% CPU.
2. During a download, speed and time left still update every second, and the summary bar updates when it finishes.
3. Pause/Resume and Clear finished still update the page straight away.
