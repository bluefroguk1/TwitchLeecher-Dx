<!-- title: Download dialog: crop preview filmstrip and tidier layout -->
## Summary

- Choosing the part to download shows START/END preview frames and a
  filmstrip of the whole VOD with draggable handles, from Twitch's seek
  preview images; HH:MM:SS boxes stay in sync, plus the resulting length and
  estimated size.
- The file name is always editable (with a reset button), the muted-parts
  warning is one line, and chat / video details are listed under Extras.

## Depends on

#106, #107, #117, #119 and the theme colours PR

Until those are merged this PR also shows their changes; please review the last commit only.

## Testing

Tested on Windows: preview frames and filmstrip, handles, time boxes, editable filename.
