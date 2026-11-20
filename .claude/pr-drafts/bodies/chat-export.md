<!-- title: Chat export as HTML (with emotes) or JSON -->
## Summary

Next to plain text, "Save chat" can write:
- HTML: names in their chat colours, timestamps and Twitch emotes as
  images (linked, or embedded so the file works offline);
- JSON: the video details and every message with its offset, name, colour,
  text and emote fragments, for other tools.
Chat text and names are HTML-encoded.

## Depends on

#107 and #117

Until those are merged this PR also shows their changes; please review the last commit only.

## Testing

Not yet tested on Windows.
