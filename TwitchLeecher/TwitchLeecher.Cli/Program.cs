using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using TwitchLeecher.Core.Enums;
using TwitchLeecher.Core.Models;
using TwitchLeecher.Services.Interfaces;
using TwitchLeecher.Services.Modules;
using TwitchLeecher.Shared.Events;

namespace TwitchLeecher.Cli;

// Command-line front end for the same services the app uses. It shares the app's settings and
// saved Twitch sign-in, so sign in once in the app first.
public static class Program
{
    private const int OK = 0;
    private const int FAILED = 1;
    private const int USAGE = 2;
    private const int NOT_SIGNED_IN = 3;

    private const string HELP = """
        Twitch Leecher command line

        Usage:
          tl search <channel> [--type broadcast|highlight|upload] [--last <n>]
          tl qualities <vod url or id>
          tl info <vod url or id>
          tl download <vod url or id> [--quality <q>] [--from <time>] [--to <time>]
                                      [--out <folder>] [--name <file name>] [--chat <format>]

        --type     video type to list (default broadcast)
        --last     number of videos to list (default 10)
        --quality  e.g. 1080p60, 720p, source or audio (default: your default quality in Settings)
        --from/--to  crop, as h:mm:ss, m:ss or seconds
        --out      folder to save to (default: the download folder in Settings)
        --name     file name (default: the file name pattern in Settings)
        --chat     also save the chat: text, html, html-emotes (emotes embedded) or json

        info prints the video's details, qualities and game chapters as JSON.

        Uses the app's settings and Twitch sign-in: sign in once in Twitch Leecher first.
        Exit codes: 0 done, 1 failed, 2 wrong usage, 3 not signed in.
        """;

    public static int Main(string[] args)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
        {
            Console.WriteLine(HELP);
            return args.Length == 0 ? USAGE : OK;
        }

        if (!TryParse(args.Skip(1).ToArray(), out string target, out Dictionary<string, string> options, out string error))
        {
            return Usage(error);
        }

        // The event aggregator dispatches "UI thread" subscriptions through the current context;
        // a console has no UI thread, so they run on the thread pool.
        SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());

        ServiceCollection services = new ServiceCollection();
        services.AddSingleton<IEventAggregator, EventAggregator>();
        // DownloadService reports finished downloads through this; a console run prints its own result instead
        ServiceModule.Load(services);
        using ServiceProvider provider = services.BuildServiceProvider();

        try
        {
            if (!IsSignedIn(provider))
            {
                Console.Error.WriteLine("Not signed in to Twitch, the sign-in has expired, or Twitch could not be reached. Sign in once in Twitch Leecher, then try again.");
                return NOT_SIGNED_IN;
            }

            return args[0] switch
            {
                "search" => Search(provider, target, options),
                "qualities" => Qualities(provider, target),
                "info" => Info(provider, target),
                "download" => Download(provider, target, options),
                _ => Usage($"Unknown command '{args[0]}'.")
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Error: " + ex.Message);
            return FAILED;
        }
    }

    private static int Search(IServiceProvider provider, string channel, Dictionary<string, string> options)
    {
        if (!TryGetVideoType(options, out VideoType videoType) || !TryGetInt(options, "last", 10, out int last))
        {
            return Usage("--type must be broadcast, highlight or upload, and --last a number.");
        }

        SearchParameters searchParams = new SearchParameters(SearchType.Channel)
        {
            Channel = channel,
            VideoType = videoType,
            LoadLimitType = LoadLimitType.LastVods,
            LoadLastVods = last,
            // ApiService reads both dates even when the limit is "last N"; they are not used for filtering then
            LoadFrom = DateTime.Today.AddDays(-30),
            LoadTo = DateTime.Today
        };

        IApiService api = provider.GetRequiredService<IApiService>();

        if (!api.ChannelExists(channel))
        {
            Console.Error.WriteLine($"Channel '{channel}' not found.");
            return FAILED;
        }

        foreach (TwitchVideo video in api.Search(searchParams))
        {
            Console.WriteLine($"{video.Id,-12} {video.RecordedDate:yyyy-MM-dd}  {Duration(video.Length),9}  {video.Views,9:N0} views  {video.Title}");
        }

        return OK;
    }

    private static int Qualities(IServiceProvider provider, string vod)
    {
        if (!TryGetVodId(vod, out string id))
        {
            return Usage($"'{vod}' is not a VOD URL or id.");
        }

        IApiService api = provider.GetRequiredService<IApiService>();
        TwitchVideoAuthInfo authInfo = api.GetVodAuthInfo(id);

        foreach (TwitchVideoQuality quality in api.GetPlaylistInfo(id, authInfo).Keys.OrderBy(q => q))
        {
            Console.WriteLine(quality.DisplayString);
        }

        return OK;
    }

    private static int Info(IServiceProvider provider, string vod)
    {
        if (!TryGetVodId(vod, out string id))
        {
            return Usage($"'{vod}' is not a VOD URL or id.");
        }

        IApiService api = provider.GetRequiredService<IApiService>();
        TwitchVideo video = api.Search(new SearchParameters(SearchType.Ids) { Ids = id }).FirstOrDefault();

        if (video == null)
        {
            Console.Error.WriteLine($"VOD {id} not found.");
            return FAILED;
        }

        TwitchVideoAuthInfo authInfo = api.GetVodAuthInfo(id);
        // a sub-only VOD lists no qualities without a subscription
        IEnumerable<TwitchVideoQuality> qualities = authInfo.SubOnly && !authInfo.Privileged
            ? []
            : api.GetPlaylistInfo(id, authInfo).Keys.OrderBy(q => q);

        JsonObject info = new JsonObject
        {
            ["id"] = video.Id,
            ["title"] = video.Title,
            ["channel"] = video.Channel,
            ["url"] = video.Url?.ToString(),
            ["recordedDate"] = video.RecordedDate.ToString("o", CultureInfo.InvariantCulture),
            ["lengthSeconds"] = (int)video.Length.TotalSeconds,
            ["views"] = video.Views,
            ["subOnly"] = authInfo.SubOnly,
            ["qualities"] = new JsonArray(qualities.Select(q => (JsonNode)q.DisplayString).ToArray()),
            ["chapters"] = new JsonArray(api.GetChapters(id)
                .Select(c => (JsonNode)new JsonObject { ["startSeconds"] = (int)c.Start.TotalSeconds, ["game"] = c.Game })
                .ToArray())
        };

        Console.WriteLine(info.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        return OK;
    }

    private static int Download(IServiceProvider provider, string vod, Dictionary<string, string> options)
    {
        if (!TryGetVodId(vod, out string id))
        {
            return Usage($"'{vod}' is not a VOD URL or id.");
        }

        TimeSpan? from = null, to = null;

        if (options.TryGetValue("from", out string fromStr) && !TryParseTime(fromStr, out from) ||
            options.TryGetValue("to", out string toStr) && !TryParseTime(toStr, out to))
        {
            return Usage("--from and --to take h:mm:ss, m:ss or seconds.");
        }

        ChatFormat? chat = null;

        if (options.TryGetValue("chat", out string chatStr))
        {
            chat = chatStr.ToLowerInvariant() switch
            {
                "text" => ChatFormat.Text,
                "html" => ChatFormat.Html,
                "html-emotes" => ChatFormat.HtmlEmbedded,
                "json" => ChatFormat.Json,
                _ => null
            };

            if (chat == null)
            {
                return Usage("--chat takes text, html, html-emotes or json.");
            }
        }

        IApiService api = provider.GetRequiredService<IApiService>();
        IPreferencesService preferencesService = provider.GetRequiredService<IPreferencesService>();
        IFilenameService filenameService = provider.GetRequiredService<IFilenameService>();
        IDownloadService downloadService = provider.GetRequiredService<IDownloadService>();

        TwitchVideo video = api.Search(new SearchParameters(SearchType.Ids) { Ids = id }).FirstOrDefault();

        if (video == null)
        {
            Console.Error.WriteLine($"VOD {id} not found.");
            return FAILED;
        }

        TwitchVideoAuthInfo authInfo = api.GetVodAuthInfo(id);

        if (!authInfo.Privileged && authInfo.SubOnly)
        {
            Console.Error.WriteLine("This video is for subscribers only.");
            return FAILED;
        }

        List<TwitchVideoQuality> qualities = api.GetPlaylistInfo(id, authInfo).Keys.OrderBy(q => q).ToList();
        Preferences prefs = preferencesService.CurrentPreferences.Clone();

        TwitchVideoQuality quality = options.TryGetValue("quality", out string wanted)
            ? FindQuality(qualities, wanted)
            : prefs.DownloadDefaultQuality.PickFrom(qualities);

        if (quality == null)
        {
            Console.Error.WriteLine($"No quality '{wanted}'. This VOD has: {string.Join(", ", qualities.Select(q => q.DisplayString))}");
            return USAGE;
        }

        string folder = options.TryGetValue("out", out string outFolder) ? Path.GetFullPath(outFolder) : prefs.DownloadFolder;
        Directory.CreateDirectory(folder);

        string filename = options.TryGetValue("name", out string name)
            ? name
            : filenameService.SubstituteWildcards(prefs.DownloadFileName, video, quality, from, to);
        filename = filenameService.EnsureExtension(filename, prefs.DownloadDisableConversion);

        DownloadParameters downloadParams = new DownloadParameters(video, qualities, quality, folder, filename, prefs.DownloadDisableConversion)
        {
            CropStart = from.HasValue,
            CropStartTime = from ?? TimeSpan.Zero,
            CropEnd = to.HasValue,
            CropEndTime = to ?? video.Length,
            DownloadChat = chat.HasValue,
            ChatFormat = chat ?? ChatFormat.Text
        };

        downloadParams.Validate();

        if (downloadParams.HasErrors)
        {
            Console.Error.WriteLine(string.Join(Environment.NewLine, downloadParams.GetErrors().Cast<string>()));
            return USAGE;
        }

        if (File.Exists(downloadParams.FullPath) || downloadService.IsFileNameUsed(downloadParams.FullPath))
        {
            Console.Error.WriteLine($"'{downloadParams.FullPath}' already exists. Pick another --name or --out.");
            return FAILED;
        }

        Console.WriteLine($"Downloading '{video.Title}' ({quality.DisplayString}{(from.HasValue || to.HasValue ? ", " + downloadParams.CroppedLengthStr : "")})");
        Console.WriteLine("to " + downloadParams.FullPath);

        downloadService.Enqueue(downloadParams);
        TwitchVideoDownload download = downloadService.Downloads.Last(d => d.DownloadParams == downloadParams);

        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            Console.Error.WriteLine(Environment.NewLine + "Cancelling...");
            downloadService.Cancel(download.Id);
        };

        return WaitFor(download);
    }

    // Prints status and progress until the download ends; the download service runs it on its own timer.
    private static int WaitFor(TwitchVideoDownload download)
    {
        string lastLine = null;
        bool live = !Console.IsOutputRedirected;

        while (true)
        {
            DownloadState state = download.DownloadState;

            if (state is DownloadState.Done or DownloadState.CompletedWithWarning or DownloadState.Error or DownloadState.Canceled)
            {
                if (live)
                {
                    Console.WriteLine();
                }

                bool ok = state is DownloadState.Done or DownloadState.CompletedWithWarning;

                if (!ok)
                {
                    Console.Error.WriteLine(download.Log);
                }

                Console.WriteLine(state.GetDescription());
                return ok ? OK : FAILED;
            }

            string line = download.IsIndeterminate || download.Progress <= 0
                ? download.Status
                : $"{download.Status} {download.Progress:0}%";

            if (line != lastLine)
            {
                if (live)
                {
                    Console.Write("\r" + line.PadRight(Math.Max(lastLine?.Length ?? 0, line.Length)));
                }
                else if (lastLine == null || !line.StartsWith(lastLine.Split(' ')[0]) || line.EndsWith("0%"))
                {
                    Console.WriteLine(line); // redirected output: status changes and every 10%
                }

                lastLine = line;
            }

            Thread.Sleep(500);
        }
    }

    private static bool IsSignedIn(IServiceProvider provider)
    {
        string token = provider.GetRequiredService<IRuntimeDataService>().RuntimeData.AuthInfo?.AccessToken;
        return !string.IsNullOrWhiteSpace(token) && provider.GetRequiredService<IApiService>().ValidateAuthentication(token, false);
    }

    private static TwitchVideoQuality FindQuality(List<TwitchVideoQuality> qualities, string wanted)
    {
        string w = wanted.Trim().ToLowerInvariant();

        if (w == "source")
        {
            return qualities.FirstOrDefault(q => q.IsSource);
        }

        if (w is "audio" or "audio only")
        {
            return qualities.FirstOrDefault(q => q.IsAudioOnly);
        }

        return qualities.FirstOrDefault(q => string.Equals(q.Name, w, StringComparison.OrdinalIgnoreCase))
               ?? qualities.FirstOrDefault(q => q.DisplayString.StartsWith(w, StringComparison.OrdinalIgnoreCase));
    }

    private static bool TryParse(string[] args, out string target, out Dictionary<string, string> options, out string error)
    {
        target = null;
        options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        error = null;

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i].StartsWith("--"))
            {
                if (i + 1 >= args.Length)
                {
                    error = $"{args[i]} needs a value.";
                    return false;
                }

                options[args[i][2..]] = args[++i];
            }
            else if (target == null)
            {
                target = args[i];
            }
            else
            {
                error = $"Unexpected '{args[i]}'.";
                return false;
            }
        }

        if (target == null)
        {
            error = "Missing the channel or VOD.";
            return false;
        }

        string[] known = ["type", "last", "quality", "from", "to", "out", "name", "chat"];
        string unknown = options.Keys.FirstOrDefault(k => !known.Contains(k, StringComparer.OrdinalIgnoreCase));

        if (unknown != null)
        {
            error = $"Unknown option --{unknown}.";
            return false;
        }

        return true;
    }

    internal static bool TryGetVodId(string vod, out string id)
    {
        Match m = Regex.Match(vod.Trim(), @"^(?:(?:https?://)?(?:www\.|m\.)?twitch\.tv/videos/)?(\d+)(?:[/?#].*)?$");
        id = m.Success ? m.Groups[1].Value : null;
        return m.Success;
    }

    internal static bool TryParseTime(string s, out TimeSpan? time)
    {
        time = null;

        if (int.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out int seconds))
        {
            time = TimeSpan.FromSeconds(seconds);
            return true;
        }

        if (TimeSpan.TryParseExact(s, [@"h\:mm\:ss", @"hh\:mm\:ss", @"m\:ss", @"mm\:ss"], CultureInfo.InvariantCulture, out TimeSpan t))
        {
            time = t;
            return true;
        }

        return false;
    }

    private static bool TryGetVideoType(Dictionary<string, string> options, out VideoType type)
    {
        type = VideoType.Broadcast;
        return !options.TryGetValue("type", out string s) || Enum.TryParse(s, true, out type) && Enum.IsDefined(type);
    }

    private static bool TryGetInt(Dictionary<string, string> options, string key, int fallback, out int value)
    {
        value = fallback;
        return !options.TryGetValue(key, out string s) || int.TryParse(s, out value) && value > 0;
    }

    private static string Duration(TimeSpan t) => $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}";

    private static int Usage(string error)
    {
        Console.Error.WriteLine(error);
        Console.Error.WriteLine("Run 'tl --help' for usage.");
        return USAGE;
    }
}

