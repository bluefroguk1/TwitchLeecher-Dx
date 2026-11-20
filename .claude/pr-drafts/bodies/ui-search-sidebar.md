<!-- title: Search page as a sidebar, opening on Search -->
## Summary

- The search form becomes a sidebar: Channel or Links / IDs (one box that
  recognises links and video ids), type chips, one "Show" dropdown and the
  Search button under the form. Enter searches from the channel field.
- Favourite channels appear as one-click chips; recent searches are listed.
- After sign-in the app opens on Search, showing the logo, name and version;
  the separate Welcome page is removed.
- The Search tab always opens the form, with a link back to the last results.
- The custom date range can't pick future dates or end before it starts.

## Depends on

the theme colours PR

Until those are merged this PR also shows their changes; please review the last commit only.

## Testing

Tested on Windows: sidebar layout, opening on Search, recent searches. Later fixes (multi-line Links / IDs box, no bottom buttons, back-to-results link) not yet re-tested.
