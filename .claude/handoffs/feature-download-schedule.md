# Handoff: feature-download-schedule

Off-peak schedule so watched-search finds (and optionally paused downloads) download only during a set time. Branch from `claude/ui-watched-searches-in-settings` (needs watched searches, per-download pause/resume and the redesigned Settings).

- **Settings > Download > OFF-PEAK SCHEDULE:** `ScheduleEnabled`, `ScheduleStartHour` / `ScheduleEndHour` (whole hours, local time, may cross midnight; equal = all day), `ScheduleIncludePaused` (default on), `SchedulePauseAtEnd` (default on). Saved under `<Download>` in config.xml.
- **Rule:** `Core/Models/DownloadSchedule.IsOpen(prefs, now)`.
- **Watched searches:** outside the window, a "Download now" find is queued paused with `DownloadParameters.WaitForSchedule = true`; the log says SCHEDULED "(starts at HH:00)". "Add to queue, paused" searches are unchanged (they wait for the user, or for the window if "Also start downloads I paused" is on).
- **`ScheduleService`** (Gui, singleton, started with watched searches), checking every 30 s:
  - When the window opens, or the app starts inside it, it resumes paused items that are `WaitForSchedule`, or all paused ones if `ScheduleIncludePaused`.
  - When the window closes and `SchedulePauseAtEnd` is on, it pauses only the items it started (marking them `WaitForSchedule` again), so downloads the user started are never touched.
- **Downloads page:** a waiting item shows the chip "SCHEDULED" and "Waits for the download schedule". A manual Resume clears `WaitForSchedule` and starts it now.
- **Not included:**
  - minute-level times (whole hours only);
  - per-day schedules;
  - scheduling ordinary manual downloads (only watched finds and paused items).

**Test needs:** waiting until a whole hour. Set From to the next hour. Script: `test-scripts\test-feature-download-schedule.ps1`.
