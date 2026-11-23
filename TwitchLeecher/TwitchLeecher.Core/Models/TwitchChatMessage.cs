using System;
using System.Collections.Generic;

namespace TwitchLeecher.Core.Models
{
    // One VOD chat message; Offset is from the start of the VOD (#71).
    // Text is the plain concatenation used by the .txt writer; Color is the chat colour ("#RRGGBB") or null;
    // Fragments keep emotes apart for the HTML writer.
    public record TwitchChatMessage(TimeSpan Offset, string Name, string Text, string Color,
        IReadOnlyList<TwitchChatFragment> Fragments);

    // EmoteId is null for plain text; Text is the emote code otherwise
    public record TwitchChatFragment(string Text, string EmoteId);
}
