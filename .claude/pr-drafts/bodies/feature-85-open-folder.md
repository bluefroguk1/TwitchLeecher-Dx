<!-- title: Downloads: Open folder for every download, not only finished ones (#85) -->
## Summary

Closes #85. The Downloads page's Open folder button was shown only once a download had finished. It now shows for queued, downloading, paused and failed downloads too.
- On Windows it opens the download folder, as before.
- On Linux the file manager still highlights the file once it exists. Before that (the file isn't there yet), it opens the folder.

## Depends on

—

If the Downloads page redesign is merged first, the same one-line change applies to its "Open folder" button.

## Testing

- Builds on Windows and Linux (GitHub Actions), and the published app starts on both.
