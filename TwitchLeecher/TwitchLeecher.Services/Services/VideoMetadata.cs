using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TwitchLeecher.Core.Models;

namespace TwitchLeecher.Services.Services
{
    // ffmpeg's FFMETADATA1 text: the title, channel, date and link players show, plus the stream's game changes as
    // chapters. Times are relative to the downloaded part [from, to] of the VOD, so they match a cropped file.
    internal static class VideoMetadata
    {
        public static string Build(TwitchVideo video, IList<(TimeSpan Start, string Game)> chapters, TimeSpan from, TimeSpan to)
        {
            StringBuilder sb = new StringBuilder(";FFMETADATA1\n");
            sb.Append("title=").Append(Escape(video.Title)).Append('\n');
            sb.Append("artist=").Append(Escape(video.Channel)).Append('\n');
            sb.Append("date=").Append(video.RecordedDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Append('\n');

            if (video.Url != null)
            {
                sb.Append("comment=").Append(Escape(video.Url.ToString())).Append('\n');
            }

            // each chapter runs until the next one starts, cut to the downloaded part
            var kept = new List<(TimeSpan Start, TimeSpan End, string Game)>();

            for (int i = 0; i < chapters.Count; i++)
            {
                TimeSpan start = chapters[i].Start < from ? from : chapters[i].Start;
                TimeSpan end = i + 1 < chapters.Count && chapters[i + 1].Start < to ? chapters[i + 1].Start : to;

                if (end > start)
                {
                    kept.Add((start, end, chapters[i].Game));
                }
            }

            // one chapter covering the whole file adds nothing
            if (kept.Count > 1)
            {
                foreach ((TimeSpan start, TimeSpan end, string game) in kept)
                {
                    sb.Append("\n[CHAPTER]\nTIMEBASE=1/1000\n");
                    sb.Append("START=").Append(((long)(start - from).TotalMilliseconds).ToString(CultureInfo.InvariantCulture)).Append('\n');
                    sb.Append("END=").Append(((long)(end - from).TotalMilliseconds).ToString(CultureInfo.InvariantCulture)).Append('\n');
                    sb.Append("title=").Append(Escape(game)).Append('\n');
                }
            }

            return sb.ToString();
        }

        // FFMETADATA1 treats = ; # \ and line breaks as special: each needs a backslash in front
        private static string Escape(string value)
        {
            StringBuilder sb = new StringBuilder();

            foreach (char c in value ?? string.Empty)
            {
                if (c == '\r')
                {
                    continue;
                }

                if (c is '=' or ';' or '#' or '\\' or '\n')
                {
                    sb.Append('\\');
                }

                sb.Append(c);
            }

            return sb.ToString();
        }
    }
}
