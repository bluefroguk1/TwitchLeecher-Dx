# Handoff: feature-compress-video

Option to compress a video after it is downloaded, to save space. Branch from `combined` (needs rebasing onto `master` once the open PRs merge).

- **Settings > Download:** `Preferences.DownloadCompress` (off by default) and `DownloadCompressLevel` (`CompressionLevel` Light / Balanced / Strong, default Balanced; `DownloadCompressLevelIndex` for the dropdown). Saved as `Compress` / `CompressLevel` in config.xml.
- **Download dialog:** "Compress after download" (`DownloadViewModel.CompressDownload` → `DownloadParameters.Compress`, a `bool?`: null = use Settings, so quick download and watched searches follow Settings). Hidden when `DownloadParameters.CanCompress` is false (conversion disabled or audio only).
- **The step:** `ProcessingService.CompressVideo` runs after conversion and before chat/VOD info. ffmpeg with Light = libx264 CRF 23, Balanced = libx265 CRF 26, Strong = libx265 CRF 30, `-preset medium`, AAC 128k, `+faststart`. It writes `<name>.compressing.mp4`, replaces the original only if smaller, logs "X -> Y". If it fails, it keeps the original and adds a warning. A cancel kills ffmpeg and deletes the temp file. Status "Compressing" with progress from ffmpeg `time=`.
- **Not included:** clips (always ready-made mp4); GPU encoders (`// shortcut:` in `CompressVideo`).
- **combined-all:** the redesigned dialog needs the checkbox in its Extras section, and the redesigned Settings needs the toggle + level in Download.

**Test needs:** one short download (3-minute crop). Script: `test-scripts\test-feature-compress-video.ps1`.
