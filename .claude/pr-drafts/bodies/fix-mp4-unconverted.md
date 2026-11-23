<!-- title: Allow .mp4 as well as .ts when conversion is off -->
## Summary

With "Disable conversion" on, the file is Twitch's own stream saved as it is. Newer VODs are fragmented MP4 (the new DASH format), but the app only accepted a `.ts` name, so they were saved as MP4 data with a `.ts` extension.

- The download dialog now accepts `.mp4` as well as `.ts` when conversion is off. The default name stays `.ts`.
- `EnsureExtension` keeps a `.mp4` name when conversion is off. It also no longer turns `name.mp4` into `name.mp4.ts`: its branch that was meant to swap the extension could never be reached.

This is the small, safe part of #101 (thanks @mpeter50). The DASH parsing in #101 is already covered by 3.9.1. Its "skip conversion when the output is .mp4" part is left out, because it would also skip conversion for older `.ts` VODs and for cropped downloads.

## Depends on

—

## Testing

Builds on Windows and Linux (GitHub Actions), and the published app starts on both. Not yet run on Windows.
