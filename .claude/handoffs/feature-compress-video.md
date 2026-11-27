# Handoff: feature-compress-video

Option to compress a video after it is downloaded, to save space. Branch from `combined` (needs rebasing onto `master` once the open PRs merge).

- **Settings > Download:** `Preferences.DownloadCompress` (off by default) and `DownloadCompressLevel` (`CompressionLevel` Light / Balanced / Strong, default Balanced; `DownloadCompressLevelIndex` for the dropdown). Saved as `Compress` / `CompressLevel` in config.xml.
- **Download dialog:** "Compress after download" (`DownloadViewModel.CompressDownload` → `DownloadParameters.Compress`, a `bool?`: null = use Settings, so quick download and watched searches follow Settings). Hidden when `DownloadParameters.CanCompress` is false (conversion disabled or audio only).
- **The step:** `ProcessingService.CompressVideo` runs after conversion and before chat/VOD info. ffmpeg with Light = libx264 CRF 23, Balanced = libx265 CRF 26, Strong = libx265 CRF 30, `-preset medium`, AAC 128k, `+faststart`. It writes `<name>.compressing.mp4`, replaces the original only if smaller, logs "X -> Y". If it fails, it keeps the original and adds a warning. A cancel kills ffmpeg and deletes the temp file. Status "Compressing" with progress from ffmpeg `time=`.
- **Not included:** clips (always ready-made mp4); GPU encoders (`// shortcut:` in `CompressVideo`).
- **combined-all:** the redesigned dialog needs the checkbox in its Extras section, and the redesigned Settings needs the toggle + level in Download.

**Test needs:** one short download (3-minute crop). Script: `test-scripts\test-feature-compress-video.ps1`.

## Added: GPU encoders

- **`Preferences.DownloadCompressEncoder`** (`CompressionEncoder` Auto / Cpu / Gpu, default Auto, saved as `CompressEncoder`) appears in Settings as an "Encoder" dropdown.
- **Detection:** Auto and Gpu look for a working encoder once per run. On Windows/Linux the candidates are `<codec>_nvenc`, then `_qsv`, then `_amf`; on macOS `_videotoolbox`. Each gets a 5-frame test encode, because ffmpeg lists hardware encoders even when the hardware is missing. The first that passes is used; otherwise the CPU (libx264/libx265) is.
- **Fallback:** if a GPU encode fails part way, it is retried on the CPU automatically.
- **Settings per encoder:** NVENC `-preset p5 -rc vbr -cq Q -b:v 0`, QSV `-global_quality Q`, AMF `-rc cqp -qp_i/p/b Q`, VideoToolbox `-q:v` mapped from Q. `// shortcut:` GPU quality reuses the CPU CRF numbers; tune per vendor if files come out too big or soft. Linux VAAPI is not included (it needs a device path).
- **Trade-off for the user:** GPU is typically 5-20x faster and leaves the CPU free, but makes files about 20-40% bigger at similar quality. CPU gives the smallest files.
- **combined-all:** the redesigned Settings › Download needs the Encoder dropdown next to the Compression level.
