<!-- title: Redesigned header and corner notifications -->
## Summary

The top bar gets labelled Search and Downloads tabs (with a count badge and
the active page underlined), a Settings button and Info/Donate/Log out icon
buttons, all with tooltips. The purple notification bar across the top is
replaced by a toast in the bottom-right corner. Sub-only setup moves from
the header into Settings.

## Depends on

the theme colours PR

Until those are merged this PR also shows their changes; please review the last commit only.

## Testing

Tested on Windows: tabs, active underline, buttons with tooltips and corner toasts in both themes. Moving sub-only setup out of the header is newer and not yet re-tested.
