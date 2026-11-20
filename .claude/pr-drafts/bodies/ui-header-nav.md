<!-- title: Redesigned header and corner notifications -->
## Summary

The top bar gets labelled Search and Downloads tabs (with a count badge and
the active page underlined), a Settings button and Info/Donate icon
buttons, all with tooltips. Clicking the logo or name goes to Search. The
purple notification bar across the top is replaced by a toast in the
bottom-right corner. Sub-only setup and Log out move from the header into
Settings, which also shows the signed-in Twitch account (taken from the
existing token validation, no extra request).

## Depends on

the theme colours PR

Until those are merged this PR also shows their changes; please review the last commit only.

## Testing

Tested on Windows: tabs, active underline, buttons with tooltips and corner toasts in both themes. Moving sub-only setup and Log out out of the header, and the logo going to Search, are newer and not yet re-tested.
