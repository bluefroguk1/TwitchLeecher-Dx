<!-- title: Add a command-line tool: tl search, tl qualities, tl download (#36) -->
## Summary

Closes #36. A new `TwitchLeecher.Cli` project builds `tl` (`tl.exe` on Windows) on the same services as the app. It uses the app's settings (download folder, file name pattern, default quality, temp folder) and its saved Twitch sign-in, so you sign in once in the app.

```text
tl search <channel> [--type broadcast|highlight|upload] [--last <n>]
tl qualities <vod url or id>
tl download <vod url or id> [--quality 1080p60|720p|source|audio] [--from <time>] [--to <time>]
                            [--out <folder>] [--name <file name>]
```

- Downloads go through the normal download service, so the temp folder, naming, conversion and crop all work as in the app. It prints status and progress, and Ctrl+C cancels.
- Exit codes for scripts: 0 done, 1 failed, 2 wrong usage, 3 not signed in.
- Sub-only VODs give "This video is for subscribers only." An existing file is never overwritten.
- The app's "quality closest to the default" logic moves from `SearchResultViewModel` into `DefaultQuality.PickFrom`, so the app and the tool share one copy.
- No new packages. Arguments are parsed with a few lines of code.

## Depends on

The Ninject removal: the tool registers the same services with `ServiceModule.Load`. This draft includes it as its first commit, so please review only the last one.

## Testing

- Builds on Windows and Linux (GitHub Actions). In CI, `tl --help` runs, a wrong option exits with 2, and running without a sign-in exits with 3.
