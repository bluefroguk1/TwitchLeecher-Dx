<!-- title: Fix check for updates: it asked for a repository that doesn't exist -->
## Summary

"Check for updates" hasn't worked since the Avalonia rewrite (5b972e0, 2023). `UpdateService` asks GitHub for the releases of `schneidermanuel/TwitchLeecher.Old-dx`, which doesn't exist. GitHub answers 404, the `catch` hides it, and the app never shows an update.

- The API address and the release page link now point at `schneidermanuel/TwitchLeecher-Dx`.
- The User-Agent and the update dialog's placeholder link (still `Franiac/TwitchLeecher.Old`) use the same name.

Worth knowing for the next release: copies of 3.9.1 and earlier still ask the old address, so they won't be told about it. Only versions with this fix will see later releases, so it may be worth mentioning in the release notes.

## Depends on

—

## Testing

- Builds on Windows and Linux (GitHub Actions), and the published app starts on both.
- Checked against GitHub: the new address answers 200 with 24 releases. The first one that isn't a draft or pre-release is v3.9.1, so a copy running 3.9.0 would be offered 3.9.1 and 3.9.1 itself would not. The release page link (`.../releases/tag/v3.9.1`) opens.
