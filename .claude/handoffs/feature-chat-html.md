# feature-chat-html

Branch: `feature-chat-html`, from `origin/feature-71-chat-pr` (upstream PR #107, `.chat.txt`). Follow-up PR on top of it.

## What changed
- `TwitchChatMessage` now also has `Color` (`"#RRGGBB"` or null) and `Fragments` (`TwitchChatFragment(Text, EmoteId)`; `EmoteId` null for plain text). `Text` stays the plain concatenation for the `.txt` writer.
- `ApiService.GetChatMessages` fills them from the same GQL page.
- New `ChatFormat` enum (`Text`, `Html`, `HtmlEmbedded`) and `DownloadParameters.ChatFormat` (default `Text` = old behaviour).
- Download dialog: the "Save chat" checkbox is now followed by a ComboBox (Text / HTML / HTML (offline)), enabled only when chat is ticked. Bound via `DownloadViewModel.ChatFormatIndex` (int, ComboBoxItem order = enum order), to avoid enum/int binding conversion.
- New `TwitchLeecher.Services/Services/ChatHtmlWriter.cs`: one self-contained `.chat.html`, inline CSS, dark background. Timestamps relative to crop start like the text writer. Names in chat colour; null colour -> Twitch default palette picked by name; colours below 0.5 luma are blended toward white. All names, text and emote ids are HTML-encoded.
- Emotes: `<img>` from `https://static-cdn.jtvnw.net/emoticons/v2/{id}/default/dark/2.0`, 28px high. Offline mode (`DownloadService.DownloadEmotes`) downloads each distinct emote once with one `WebClient` and inlines it as a `data:` URI using the server's Content-Type (animated emotes are GIF). A failed emote falls back to `:code:` text.
- Errors unchanged: chat failure adds "Chat could not be saved" and keeps the video.

## GQL shape (checked with curl, anonymous, `Constants.ClientIdWeb`, 2026-11-20)
`data.video.comments.edges[].node`:
- `commenter`: `{id, login, displayName}` or null (deleted user)
- `contentOffsetSeconds`, `createdAt`, `id`
- `message.fragments[]`: `{text, emote}`; `emote` is null or `{id: "425618;37;39", emoteID: "425618", from: 37}`; `text` is the emote code for emote fragments
- `message.userColor`: `"#RRGGBB"` or null; `message.userBadges[]` (not used)

## Not done
- No compile here (no .NET SDK on the Mac); written carefully but unbuilt.
- `// shortcut:` 7TV/BTTV/FFZ emotes not included; adding them means fetching each provider's channel emote set and matching words.
- Badges are not shown.
- No shared `HttpClient` exists on this branch, so emotes use one `WebClient` like the rest of `DownloadService`.

## Merge into combined-all
- The dialog change is one ComboBox inside the "Save chat" StackPanel in `DownloadView.axaml`; move it next to the chat checkbox in the redesigned layout and keep `{DynamicResource}` brushes.

## Test
- `test-scripts/test-feature-chat-html.ps1`
- Test needs: one short cropped download with chat. Open the HTML in a browser and check names, colours, emotes, and offline mode with the network off.

## Added after second test round

- New format **JSON** (`ChatFormat.Json`, index 3, appended so saved indexes keep their meaning): writes `<video>.chat.json` with the video (id, title, channel, crop start/end) and `messages` (offsetSeconds and HH:MM:SS from the crop start, name, colour, text, fragments with emoteId).
