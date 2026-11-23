<!-- title: Option to compress videos after download -->
## Summary

Settings > Download gets "Compress videos after download" with a level
(H.264 CRF 23, H.265 CRF 26, H.265 CRF 30) and an encoder choice: Auto
(graphics card when one works, else CPU), CPU (smallest files) or graphics
card (fastest). GPU encoders (NVENC, Quick Sync, AMF, VideoToolbox) are
checked with a tiny test encode; a failed GPU encode, or one whose result
is not smaller, is retried on the CPU. GPU quality values are mapped so each
level gives about the same size as the CPU encoder.
The download dialog can turn compression on or off per download. The file
is only replaced if the result is smaller, and cancelling stops the encode.

## Depends on

the open PRs

Until those are merged this PR also shows their changes; please review the last commit only.

## Testing

Tested on Windows 11 (2026-11-23): CPU and NVIDIA (NVENC) compression of a downloaded VOD; the result replaces the file only when smaller.
