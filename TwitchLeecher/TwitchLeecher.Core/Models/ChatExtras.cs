using System;
using System.Collections.Generic;

namespace TwitchLeecher.Core.Models
{
    // Chat images besides Twitch's own emotes, for the HTML chat: BTTV/FFZ/7TV emotes by code, badges by
    // "setID/version", and cheermote tiers by prefix. Src is a link, or a data: URI once embedded.
    public class ChatExtras
    {
        public Dictionary<string, ChatImage> Emotes { get; } = new Dictionary<string, ChatImage>(StringComparer.Ordinal);

        public Dictionary<string, ChatImage> Badges { get; } = new Dictionary<string, ChatImage>(StringComparer.Ordinal);

        // tiers sorted by Bits, lowest first
        public Dictionary<string, List<CheerTier>> Cheermotes { get; } =
            new Dictionary<string, List<CheerTier>>(StringComparer.OrdinalIgnoreCase);
    }

    // Overlay: a "zero-width" emote, drawn on top of the emote before it. Title: shown on hover.
    public record ChatImage(string Src, bool Overlay = false, string Title = null);

    // Color: Twitch's colour for this tier's amount ("#rrggbb")
    public record CheerTier(int Bits, string Src, string Color);
}
