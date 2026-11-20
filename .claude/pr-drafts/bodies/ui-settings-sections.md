<!-- title: Settings with a side menu, filename chips and a save bar -->
## Summary

- Sections in a side menu (General, Search, Download). General shows the
  signed-in Twitch account with Log out, and the theme.
- The filename template gets clickable wildcard chips and a live preview
  of the resulting name, replacing the long wildcard table.
- A save bar appears only when something changed (Reset, Undo, Save).
- Plain-English toggles, with the temporary folder and connection count
  alongside the other download settings.

## Depends on

#108, #118 and the theme colours PR

Until those are merged this PR also shows their changes; please review the last commit only.

## Testing

Tested on Windows: side menu, chips, preview, save bar. Removing the favourites list is newer and not yet re-tested.
