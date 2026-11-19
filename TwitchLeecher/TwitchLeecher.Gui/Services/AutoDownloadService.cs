using System.Text.Json;
using Avalonia.Threading;
using TwitchLeecher.Core.Enums;
using TwitchLeecher.Core.Models;
using TwitchLeecher.Gui.Interfaces;
using TwitchLeecher.Services.Interfaces;

namespace TwitchLeecher.Gui.Services
{
    // Channel monitoring: every few minutes, queues new VODs (and optionally clips) of the watched channels.
    internal class AutoDownloadService : IAutoDownloadService
    {
        #region Constants

        private const string SEEN_FILE = "auto_download_seen.json";
        private const string LOG_FILE = "auto_download.log";
        private const long LOG_MAX_BYTES = 1024 * 1024;

        private const int VODS_PER_CHECK = 20;
        private const int CLIP_DAYS = 7;
        private const int MIN_INTERVAL = 5;

        #endregion Constants

        #region Fields

        private readonly IApiService _apiService;
        private readonly IQuickDownloadService _quickDownloadService;
        private readonly IPreferencesService _preferencesService;
        private readonly IFolderService _folderService;
        private readonly ILogService _logService;
        private readonly INotificationService _notificationService;

        private int _started;

        // key: lower-case channel name for VODs, plus "#clips" for clips; value: handled video/clip ids.
        // Only touched by the loop task, so no locking.
        private Dictionary<string, HashSet<string>> _seen;

        #endregion Fields

        #region Constructors

        public AutoDownloadService(
            IApiService apiService,
            IQuickDownloadService quickDownloadService,
            IPreferencesService preferencesService,
            IFolderService folderService,
            ILogService logService,
            INotificationService notificationService)
        {
            _apiService = apiService;
            _quickDownloadService = quickDownloadService;
            _preferencesService = preferencesService;
            _folderService = folderService;
            _logService = logService;
            _notificationService = notificationService;
        }

        #endregion Constructors

        #region Methods

        public void Start()
        {
            if (Interlocked.Exchange(ref _started, 1) == 0)
            {
                Task.Run(RunAsync);
            }
        }

        private async Task RunAsync()
        {
            DateTime lastCheck = DateTime.MinValue;

            while (true)
            {
                try
                {
                    Preferences prefs = _preferencesService.CurrentPreferences;

                    if (!prefs.AutoDownloadEnabled)
                    {
                        // check straight away once it is switched on
                        lastCheck = DateTime.MinValue;
                    }
                    else if (DateTime.Now >= lastCheck.AddMinutes(Math.Max(MIN_INTERVAL, prefs.AutoDownloadInterval)))
                    {
                        lastCheck = DateTime.Now;
                        await CheckChannels(prefs);
                    }
                }
                catch (Exception ex)
                {
                    Log("Check failed, retrying next time: " + ex.Message);
                    _logService.LogException(ex);
                }

                // wake every minute so settings changes apply without a restart
                await Task.Delay(TimeSpan.FromMinutes(1));
            }
        }

        private async Task CheckChannels(Preferences prefs)
        {
            _seen ??= LoadSeen();

            string[] channels = prefs.GetAutoDownloadChannels();

            if (channels.Length == 0)
            {
                Log("Auto download is on but no channels are set");
                return;
            }

            foreach (string channel in channels)
            {
                // one failing channel (network error, renamed channel) must not stop the others
                try
                {
                    await CheckVideos(channel, channel.ToLowerInvariant(), VideoType.Broadcast, v => !v.Live);

                    if (prefs.AutoDownloadClips)
                    {
                        int minViews = prefs.AutoDownloadClipMinViews;
                        await CheckVideos(channel, channel.ToLowerInvariant() + "#clips", VideoType.Clip, v => v.Views >= minViews);
                    }
                }
                catch (Exception ex)
                {
                    Log($"{channel}: check failed, retrying next time: {ex.Message}");
                }
            }
        }

        // isReady == false leaves the video unseen so a later check picks it up (live VOD, clip below the view threshold).
        private async Task CheckVideos(string channel, string key, VideoType videoType, Func<TwitchVideo, bool> isReady)
        {
            SearchParameters searchParams = new SearchParameters(SearchType.Channel)
            {
                Channel = channel,
                VideoType = videoType,
                // without dates Helix lists clips by all-time views, so ask for recent ones
                LoadLimitType = videoType == VideoType.Clip ? LoadLimitType.Timespan : LoadLimitType.LastVods,
                LoadFrom = DateTime.Now.Date.AddDays(-CLIP_DAYS),
                LoadTo = DateTime.Now.Date,
                LoadLastVods = VODS_PER_CHECK
            };

            List<TwitchVideo> videos = _apiService.Search(searchParams).ToList();
            string what = videoType == VideoType.Clip ? "clip" : "VOD";

            if (!_seen.TryGetValue(key, out HashSet<string> seen))
            {
                // first check of this channel: what is there already is history, only newer videos get downloaded
                _seen[key] = new HashSet<string>(videos.Select(v => v.Id));
                SaveSeen();
                Log($"{channel}: now watching, {videos.Count} existing {what}(s) marked as seen, not downloaded");
                return;
            }

            foreach (TwitchVideo video in videos.Where(v => !seen.Contains(v.Id) && isReady(v)))
            {
                DownloadParameters downloadParams = _quickDownloadService.CreateDownloadParams(video, out string problem);

                // the download queue is bound to the UI
                string notQueued = downloadParams == null
                    ? problem
                    : await Dispatcher.UIThread.InvokeAsync(() => _quickDownloadService.TryEnqueue(downloadParams));

                // shortcut: a skipped video (sub-only, file exists, bad download settings) is not retried, the log says why; add a retry list if users ask
                seen.Add(video.Id);
                SaveSeen();

                if (notQueued != null)
                {
                    Log($"{channel}: skipped {what} {video.Id} \"{video.Title}\": {notQueued}");
                    continue;
                }

                Log($"{channel}: queued {what} {video.Id} \"{video.Title}\"");

                string message = $"Auto download: {channel} - {video.Title}";
                Dispatcher.UIThread.Post(() => _notificationService.ShowNotification(message));

                // DESKTOP NOTIFICATION: call the tray/desktop notification service here once it exists.
            }
        }

        private string SeenFile => Path.Combine(_folderService.GetAppDataFolder(), SEEN_FILE);

        private Dictionary<string, HashSet<string>> LoadSeen()
        {
            try
            {
                if (File.Exists(SeenFile))
                {
                    return JsonSerializer.Deserialize<Dictionary<string, HashSet<string>>>(File.ReadAllText(SeenFile))
                           ?? new Dictionary<string, HashSet<string>>();
                }
            }
            catch (Exception ex)
            {
                // starting empty is safe: every channel counts as new, so its videos are marked seen, not downloaded again
                Log("Could not read " + SEEN_FILE + ", starting fresh: " + ex.Message);
            }

            return new Dictionary<string, HashSet<string>>();
        }

        // shortcut: the seen ids are never pruned (a few bytes per video), prune by date if the file ever gets big
        private void SaveSeen()
        {
            Directory.CreateDirectory(_folderService.GetAppDataFolder());
            File.WriteAllText(SeenFile, JsonSerializer.Serialize(_seen));
        }

        private void Log(string message)
        {
            try
            {
                string logFile = Path.Combine(_folderService.GetAppDataFolder(), LOG_FILE);

                if (File.Exists(logFile) && new FileInfo(logFile).Length > LOG_MAX_BYTES)
                {
                    File.Delete(logFile);
                }

                Directory.CreateDirectory(_folderService.GetAppDataFolder());
                File.AppendAllText(logFile, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}{Environment.NewLine}");
            }
            catch
            {
                // logging must never stop the loop
            }
        }

        #endregion Methods
    }
}
