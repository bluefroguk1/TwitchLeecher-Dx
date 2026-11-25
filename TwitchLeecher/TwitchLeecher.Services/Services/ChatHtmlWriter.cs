using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using TwitchLeecher.Core.Models;
using TwitchLeecher.Shared.Extensions;

namespace TwitchLeecher.Services.Services
{
    // Renders VOD chat as one self-contained HTML page. Chat is untrusted: every name, text and emote id is encoded.
    internal static class ChatHtmlWriter
    {
        private static readonly Regex HexColor = new Regex("^#[0-9A-Fa-f]{6}$");

        // Twitch's default name colours, used when a user never picked one
        private static readonly string[] Palette =
        {
            "#FF0000", "#0000FF", "#008000", "#B22222", "#FF7F50", "#9ACD32", "#FF4500", "#2E8B57",
            "#DAA520", "#D2691E", "#5F9EA0", "#1E90FF", "#FF69B4", "#8A2BE2", "#00FF7F"
        };

        // highRes: Twitch's largest size (3.0), for sharp emotes when zoomed in; 2.0 otherwise
        public static string EmoteUrl(string emoteId, bool highRes = false)
        {
            return "https://static-cdn.jtvnw.net/emoticons/v2/" + Uri.EscapeDataString(emoteId) + "/default/dark/" +
                   (highRes ? "3.0" : "2.0");
        }

        private static readonly Regex Cheer = new Regex("^([A-Za-z]+)([0-9]+)$");

        // emoteSources maps a Twitch emote id -> img src. Null links every emote to the CDN;
        // an id missing from the map (failed download) falls back to the :code: text.
        // extras (may be null): BTTV/FFZ/7TV emotes, badges and cheermotes, already filtered to what is shown.
        public static string Write(string title, IEnumerable<TwitchChatMessage> messages, TimeSpan from,
            IReadOnlyDictionary<string, string> emoteSources, ChatExtras extras = null, bool highRes = false)
        {
            StringBuilder sb = new StringBuilder();

            sb.Append("<!DOCTYPE html>\n<html><head><meta charset=\"utf-8\"><title>")
                .Append(WebUtility.HtmlEncode(title)).Append(" - Chat</title><style>\n")
                .Append("body{background:#18181b;color:#efeff1;font:14px/1.6 \"Segoe UI\",Roboto,\"Helvetica Neue\",Arial,sans-serif;margin:0;padding:16px}\n")
                .Append("p{margin:0;padding:2px 0;overflow-wrap:anywhere}\n")
                .Append(".t{color:#adadb8;font-variant-numeric:tabular-nums;margin-right:6px}\n")
                .Append(".n{font-weight:600}\n")
                .Append("img{height:28px;vertical-align:middle;margin:-4px 0}\n")
                // an emote and the zero-width emotes drawn over it share one box
                .Append(".e{position:relative;display:inline-block}\n")
                .Append(".e .z{position:absolute;left:50%;top:0;transform:translateX(-50%)}\n")
                .Append(".g{height:18px;margin:0 3px 0 0}\n")
                .Append(".b{font-weight:700;margin-left:2px}\n")
                .Append("</style></head><body>\n");

            foreach (TwitchChatMessage m in messages)
            {
                sb.Append("<p><span class=\"t\">[").Append((m.Offset - from).ToDaylessString()).Append("]</span>");

                foreach (string badge in m.Badges ?? Array.Empty<string>())
                {
                    if (extras != null && extras.Badges.TryGetValue(badge, out ChatImage image))
                    {
                        AppendImg(sb, image.Src, image.Title ?? badge, "g");
                    }
                }

                sb.Append("<span class=\"n\" style=\"color:").Append(ReadableColor(m.Color, m.Name)).Append("\">")
                    .Append(WebUtility.HtmlEncode(m.Name)).Append("</span>: ");

                // where the last emote's box ends, so a zero-width emote right after it can go inside; -1 = no emote just before
                int emoteEnd = -1;

                void Emote(string src, string code)
                {
                    sb.Append("<span class=\"e\">");
                    AppendImg(sb, src, code);
                    sb.Append("</span>");
                    emoteEnd = sb.Length;
                }

                foreach (TwitchChatFragment f in m.Fragments)
                {
                    string src = null;

                    if (f.EmoteId == null)
                    {
                        string[] words = f.Text.Split(' ');

                        for (int i = 0; i < words.Length; i++)
                        {
                            string word = words[i];
                            ChatImage tp = null;
                            Match cheer = Cheer.Match(word);

                            if (extras != null && extras.Emotes.TryGetValue(word, out tp) && tp.Overlay && emoteEnd >= 0)
                            {
                                // on top of the emote before: drop the space and put it inside that emote's box
                                sb.Length = emoteEnd;
                                string overlay = Img(tp.Src, word, "z");
                                sb.Insert(emoteEnd - "</span>".Length, overlay);
                                emoteEnd += overlay.Length;
                                continue;
                            }

                            if (i > 0)
                            {
                                sb.Append(' ');
                            }

                            if (tp != null)
                            {
                                Emote(tp.Src, word);
                            }
                            else if (cheer.Success && extras != null &&
                                     extras.Cheermotes.TryGetValue(cheer.Groups[1].Value, out List<CheerTier> tiers) &&
                                     int.TryParse(cheer.Groups[2].Value, out int bits) &&
                                     tiers.LastOrDefault(t => t.Bits <= bits) is CheerTier tier)
                            {
                                AppendImg(sb, tier.Src, word);
                                sb.Append("<span class=\"b\" style=\"color:")
                                    .Append(HexColor.IsMatch(tier.Color ?? "") ? tier.Color : "#efeff1").Append("\">")
                                    .Append(bits.ToString(CultureInfo.InvariantCulture)).Append("</span>");
                                emoteEnd = -1;
                            }
                            else
                            {
                                sb.Append(WebUtility.HtmlEncode(word));

                                if (word.Length > 0)
                                {
                                    emoteEnd = -1;
                                }
                            }
                        }
                    }
                    else if (emoteSources == null || emoteSources.TryGetValue(f.EmoteId, out src))
                    {
                        Emote(src ?? EmoteUrl(f.EmoteId, highRes), f.Text);
                    }
                    else
                    {
                        sb.Append(WebUtility.HtmlEncode(":" + f.Text + ":"));
                        emoteEnd = -1;
                    }
                }

                sb.Append("</p>\n");
            }

            return sb.Append("</body></html>\n").ToString();
        }

        private static void AppendImg(StringBuilder sb, string src, string title, string cssClass = null)
        {
            sb.Append(Img(src, title, cssClass));
        }

        private static string Img(string src, string title, string cssClass = null)
        {
            string alt = WebUtility.HtmlEncode(title);
            return "<img" + (cssClass == null ? "" : " class=\"" + cssClass + "\"") + " src=\"" + WebUtility.HtmlEncode(src) +
                   "\" alt=\"" + alt + "\" title=\"" + alt + "\">";
        }

        // Returns "#RRGGBB" readable on the dark background: the user's colour (or a stable palette pick), lightened if too dark
        internal static string ReadableColor(string color, string name)
        {
            if (color == null || !HexColor.IsMatch(color))
            {
                int sum = 0;

                foreach (char c in name ?? string.Empty)
                {
                    sum += c;
                }

                color = Palette[sum % Palette.Length];
            }

            int rgb = int.Parse(color.Substring(1), NumberStyles.HexNumber);
            double r = (rgb >> 16) & 0xFF, g = (rgb >> 8) & 0xFF, b = rgb & 0xFF;
            double luma = (0.299 * r + 0.587 * g + 0.114 * b) / 255;
            const double minLuma = 0.5;

            if (luma < minLuma)
            {
                // blending toward white raises luma linearly, so this lands exactly on minLuma
                double t = (minLuma - luma) / (1 - luma);
                r += (255 - r) * t;
                g += (255 - g) * t;
                b += (255 - b) * t;
            }

            return "#" + ((int)Math.Round(r)).ToString("X2") + ((int)Math.Round(g)).ToString("X2") +
                   ((int)Math.Round(b)).ToString("X2");
        }
    }
}
