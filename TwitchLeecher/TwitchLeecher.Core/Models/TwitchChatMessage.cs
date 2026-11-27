using System;
using System.Collections.Generic;

namespace TwitchLeecher.Core.Models
{
    // One VOD chat message; Offset is from the start of the VOD (#71).
    // Text is the plain concatenation used by the .txt writer; Color is the chat colour ("#RRGGBB") or null;
    // Fragments keep emotes apart for the HTML writer. Badges are the sender's, as "setID/version" (null if none known).
    public record TwitchChatMessage(TimeSpan Offset, string Name, string Text, string Color,
        IReadOnlyList<TwitchChatFragment> Fragments, IReadOnlyList<string> Badges = null);

    // EmoteId is null for plain text; Text is the emote code otherwise
    public record TwitchChatFragment(string Text, string EmoteId);
}
