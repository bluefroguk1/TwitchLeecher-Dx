using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia.Threading;
using TwitchLeecher.Core.Enums;
using TwitchLeecher.Core.Models;
using TwitchLeecher.Gui.Interfaces;
using TwitchLeecher.Services.Interfaces;

namespace TwitchLeecher.Gui.Services
{
    // Watched searches: every few minutes, re-runs saved channel searches and queues new results.
    internal class AutoDownloadService : IAutoDownloadService
    {
        #region Constants

        private const string SEARCHES_FILE = "watched_searches.json";
        private const string SEEN_FILE = "auto_download_seen.json";
        private const string LOG_FILE = "auto_download.log";
        private const long LOG_MAX_BYTES = 1024 * 1024;

        private const int VODS_PER_CHECK = 20;
        private const int CLIP_DAYS = 7;
        private const int MIN_INTERVAL = 5;
        private const int DEFAULT_INTERVAL = 15;

        #endregion Constants

        #region Fields

        private readonly IApiService _apiService;
        private readonly IQuickDownloadService _quickDownloadService;
        private readonly IPreferencesService _preferencesService;
        private readonly IFolderService _folderService;
        private readonly ILogService _logService;
        private readonly INotificationService _notificationService;

        // released to wake the loop early (Check now, switched on)
        private readonly SemaphoreSlim _wake = new SemaphoreSlim(0);

        private bool _enabled;
        private int _interval = DEFAULT_INTERVAL;
        private int _started;
        private int _checkNow;
        private DateTime? _lastCheck;

        // key: WatchedSearch.Key; value: handled video/clip ids. Only touched by the loop task, so no locking.
        private Dictionary<string, HashSet<string>> _seen;

        // live VODs already logged as WAITING, so each is logged once instead of every check
        private readonly HashSet<string> _waitingLogged = new HashSet<string>();

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

            Searches = new ObservableCollection<WatchedSearch>();
            LoadSearches();

            foreach (WatchedSearch search in Searches)
            {
                search.PropertyChanged += Search_PropertyChanged;
            }

            Searches.CollectionChanged += Searches_CollectionChanged;
        }

        #endregion Constructors

        #region Properties

        public ObservableCollection<WatchedSearch> Searches { get; }

        public bool Enabled
        {
            get => _enabled;
            set
            {
                if (_enabled == value)
                {
                    return;
                }

                _enabled = value;
                SaveSearches();

                if (value)
                {
                    // check straight away once it is switched on
                    _wake.Release();
                }
            }
        }

        public int Interval
        {
            get => _interval;
            set
            {
                int interval = Math.Max(MIN_INTERVAL, value);

                if (_interval != interval)
                {
                    _interval = interval;
                    SaveSearches();
                }
            }
        }

        public DateTime? LastCheck => _lastCheck;

        public event EventHandler Changed;

        private string AppDataFolder => _folderService.GetAppDataFolder();

        private string SearchesFile => Path.Combine(AppDataFolder, SEARCHES_FILE);

        private string SeenFile => Path.Combine(AppDataFolder, SEEN_FILE);

        private string LogFile => Path.Combine(AppDataFolder, LOG_FILE);

        #endregion Properties

        #region Methods

        public void Start()
        {
            if (Interlocked.Exchange(ref _started, 1) == 0)
            {
                Task.Run(RunAsync);
            }
        }

        public void CheckNow()
        {
            Interlocked.Exchange(ref _checkNow, 1);
            _wake.Release();
        }

        public WatchedSearch Add(string channel, VideoType videoType)
        {
            WatchedSearch search = new WatchedSearch { Channel = channel, VideoType = videoType };

            WatchedSearch existing = string.IsNullOrEmpty(search.Channel)
                ? null
                : Searches.FirstOrDefault(s => s.Key == search.Key);

            if (existing != null)
            {
                existing.Active = true;
                return existing;
            }

            Searches.Add(search);
            return search;
        }

        public List<WatchedActivity> ReadActivity(int max)
        {
            List<WatchedActivity> activity = new List<WatchedActivity>();

            try
            {
                if (!File.Exists(LogFile))
                {
                    return activity;
                }

                string[] lines = File.ReadAllLines(LogFile);

                for (int i = lines.Length - 1; i >= 0 && activity.Count < max; i--)
                {
                    // time \t KIND \t search \t message; lines from the old channel-list format have no tabs and are skipped
                    string[] parts = lines[i].Split('\t', 4);

                    if (parts.Length == 4 && DateTime.TryParse(parts[0], out DateTime time))
                    {
                        activity.Add(new WatchedActivity(time, parts[1], parts[2], parts[3]));
                    }
                }
            }
            catch (Exception ex)
            {
                _logService.LogException(ex);
            }

            return activity;
        }

        private async Task RunAsync()
        {
            DateTime lastRun = DateTime.MinValue;

            while (true)
            {
                try
                {
                    bool force = Interlocked.Exchange(ref _checkNow, 0) == 1;

                    if (!Enabled && !force)
                    {
                        // check straight away once it is switched on
                        lastRun = DateTime.MinValue;
                    }
                    else if (force || DateTime.Now >= lastRun.AddMinutes(Interval))
                    {
                        lastRun = DateTime.Now;
                        await CheckSearches();
                        _lastCheck = DateTime.Now;
                        RaiseChanged();
                    }
                }
                catch (Exception ex)
                {
                    Log("ERROR", "All searches", "Check failed, retrying next time: " + ex.Message);
                    _logService.LogException(ex);
                }

                // wake every minute so interval changes apply without a restart
                await _wake.WaitAsync(TimeSpan.FromMinutes(1));
            }
        }

        private async Task CheckSearches()
        {
            _seen ??= LoadSeen();

            // the collection is bound to the UI, so take the snapshot there
            List<WatchedSearch> searches = await Dispatcher.UIThread.InvokeAsync(() =>
                Searches.Where(s => s.Active && !string.IsNullOrEmpty(s.Channel)).ToList());

            foreach (WatchedSearch search in searches)
            {
                // one failing search (network error, renamed channel) must not stop the others
                try
                {
                    await CheckSearch(search);
                }
                catch (Exception ex)
                {
                    Log("ERROR", search.Label, "Check failed, retrying next time: " + ex.Message);
                }
            }
        }

        // Only finished VODs may be downloaded. Twitch now gives a VOD that is still recording a normal thumbnail,
        // so TwitchVideo.Live alone misses it: ask Helix whether the channel is streaming, and treat broadcasts
        // created since that stream started as live. If that check fails, the newest broadcast is held back too.
        private Func<TwitchVideo, bool> LiveCheck(string channel, VideoType videoType, List<TwitchVideo> videos, string label)
        {
            if (videoType != VideoType.Broadcast)
            {
                // highlights, uploads and clips are always complete
                return v => false;
            }

            DateTime? streamStart;

            try
            {
                streamStart = _apiService.GetLiveStreamStart(channel);
            }
            catch (Exception ex)
            {
                TwitchVideo newest = videos.OrderByDescending(v => v.RecordedDate).FirstOrDefault();
                Log("ERROR", label, "Could not check whether the channel is live, holding back the newest VOD: " + ex.Message);
                return v => v.Live || (newest != null && v.Id == newest.Id);
            }

            if (streamStart == null)
            {
                return v => v.Live;
            }

            // shortcut: a stream restarted within this tolerance can make the previous VOD look live for one check
            DateTime from = streamStart.Value.AddMinutes(-10);
            return v => v.Live || v.RecordedDate.ToUniversalTime() >= from;
        }

        private async Task CheckSearch(WatchedSearch search)
        {
            // read once: the user may edit the search on the UI thread while this runs
            string channel = search.Channel;
            string key = search.Key;
            string label = search.Label;
            VideoType videoType = search.VideoType;
            int minViews = search.MinViews;
            bool startPaused = search.StartPaused;
            DefaultQuality quality = search.GetQuality();
            bool isClip = videoType == VideoType.Clip;

            SearchParameters searchParams = new SearchParameters(SearchType.Channel)
            {
                Channel = channel,
                VideoType = videoType,
                // without dates Helix lists clips by all-time views, so ask for recent ones
                LoadLimitType = isClip ? LoadLimitType.Timespan : LoadLimitType.LastVods,
                LoadFrom = DateTime.Now.Date.AddDays(-CLIP_DAYS),
                LoadTo = DateTime.Now.Date,
                LoadLastVods = VODS_PER_CHECK
            };

            List<TwitchVideo> videos = _apiService.Search(searchParams).ToList();

            Func<TwitchVideo, bool> isLive = LiveCheck(channel, videoType, videos, label);

            if (!_seen.TryGetValue(key, out HashSet<string> seen))
            {
                // first check of this search: finished videos are history, only newer ones get downloaded.
                // A stream that is live right now is left unseen, so its VOD downloads once the stream ends.
                List<TwitchVideo> finished = videos.Where(v => !isLive(v)).ToList();
                _seen[key] = new HashSet<string>(finished.Select(v => v.Id));
                SaveSeen();
                bool liveNow = finished.Count < videos.Count;
                Log("FIRST RUN", label, $"{finished.Count} existing videos marked as seen, not downloaded" +
                                        (liveNow ? "; the current live stream will download when it ends" : ""));
                return;
            }

            foreach (TwitchVideo video in videos.Where(v => !seen.Contains(v.Id)))
            {
                // not ready: left unseen so a later check picks it up
                if (isClip ? video.Views < minViews : isLive(video))
                {
                    if (!isClip && _waitingLogged.Add(video.Id))
                    {
                        Log("WAITING", label, $"{video.Title} (still live, retry next check)");
                    }

                    continue;
                }

                DownloadParameters downloadParams =
                    _quickDownloadService.CreateDownloadParams(video, out string problem, quality);

                // off-peak schedule: outside the window the find waits, paused, until the window opens
                bool holdForSchedule = !startPaused &&
                                       !DownloadSchedule.IsOpen(_preferencesService.CurrentPreferences, DateTime.Now);

                if (downloadParams != null)
                {
                    downloadParams.FromWatchedSearch = true;
                    downloadParams.WaitForSchedule = holdForSchedule;
                }

                // the download queue is bound to the UI
                string notQueued = downloadParams == null
                    ? problem
                    : await Dispatcher.UIThread.InvokeAsync(() =>
                        _quickDownloadService.TryEnqueue(downloadParams, startPaused || holdForSchedule));

                // shortcut: a skipped video (sub-only, file exists, bad download settings) is not retried, the log says why; add a retry list if users ask
                seen.Add(video.Id);
                SaveSeen();

                if (notQueued != null)
                {
                    Log("SKIPPED", label, $"{video.Title}: {notQueued}");
                    continue;
                }

                if (holdForSchedule)
                {
                    Log("SCHEDULED", label,
                        $"{video.Title} (starts at {DownloadSchedule.Describe(_preferencesService.CurrentPreferences).Substring(0, 5)})");
                }
                else if (startPaused)
                {
                    Log("PAUSED", label, $"{video.Title} (waiting for you in Downloads)");
                }
                else
                {
                    Log("STARTED", label, video.Title);
                }

                // DESKTOP NOTIFICATION: call the tray/desktop notification service here once it exists.
            }
        }

        private void RaiseChanged()
        {
            Dispatcher.UIThread.Post(() => Changed?.Invoke(this, EventArgs.Empty));
        }

        private void Searches_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            foreach (WatchedSearch search in e.OldItems?.OfType<WatchedSearch>() ?? Enumerable.Empty<WatchedSearch>())
            {
                search.PropertyChanged -= Search_PropertyChanged;
            }

            foreach (WatchedSearch search in e.NewItems?.OfType<WatchedSearch>() ?? Enumerable.Empty<WatchedSearch>())
            {
                search.PropertyChanged += Search_PropertyChanged;
            }

            SaveSearches();
        }

        private void Search_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // LatestResult and IsEditing are display-only
            if (e.PropertyName is nameof(WatchedSearch.Channel) or nameof(WatchedSearch.VideoType)
                or nameof(WatchedSearch.MinViews) or nameof(WatchedSearch.Quality)
                or nameof(WatchedSearch.Active) or nameof(WatchedSearch.StartPaused))
            {
                SaveSearches();
            }
        }

        private void LoadSearches()
        {
            try
            {
                if (File.Exists(SearchesFile))
                {
                    SearchesData data = JsonSerializer.Deserialize(File.ReadAllText(SearchesFile), WatchedSearchesJson.Default.SearchesData);

                    if (data != null)
                    {
                        _enabled = data.Enabled;
                        _interval = Math.Max(MIN_INTERVAL, data.Interval);

                        foreach (WatchedSearch search in data.Searches ?? new List<WatchedSearch>())
                        {
                            Searches.Add(search);
                        }

                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                // keep the unreadable file for the user instead of overwriting it with the migrated list
                _logService.LogException(ex);
                TryBackup(SearchesFile);
            }

            MigrateFromChannelList();
        }

        // Before watched searches there was one comma-separated channel list plus global clip settings in config.xml.
        // Each old channel becomes a Broadcasts search, plus a Clips search if clips were on. config.xml is left as it was,
        // so an older build still finds its settings.
        private void MigrateFromChannelList()
        {
            try
            {
                Preferences prefs = _preferencesService.CurrentPreferences;

                _enabled = prefs.AutoDownloadEnabled;
                _interval = Math.Max(MIN_INTERVAL, prefs.AutoDownloadInterval);

                foreach (string channel in prefs.GetAutoDownloadChannels())
                {
                    Searches.Add(new WatchedSearch { Channel = channel, VideoType = VideoType.Broadcast });

                    if (prefs.AutoDownloadClips)
                    {
                        Searches.Add(new WatchedSearch
                        {
                            Channel = channel,
                            VideoType = VideoType.Clip,
                            MinViews = prefs.AutoDownloadClipMinViews
                        });
                    }
                }

                SaveSearches();
            }
            catch (Exception ex)
            {
                _logService.LogException(ex);
            }
        }

        private static void TryBackup(string file)
        {
            try
            {
                File.Copy(file, file + ".bak", true);
            }
            catch
            {
                // nothing more to do; the original is still there until the next save
            }
        }

        private void SaveSearches()
        {
            try
            {
                SearchesData data = new SearchesData
                {
                    Enabled = _enabled,
                    Interval = _interval,
                    Searches = Searches.ToList()
                };

                Directory.CreateDirectory(AppDataFolder);

                // write then rename, so a crash mid-save never leaves a half-written file
                string tempFile = SearchesFile + ".tmp";
                File.WriteAllText(tempFile, JsonSerializer.Serialize(data, WatchedSearchesJson.Default.SearchesData));
                File.Move(tempFile, SearchesFile, true);
            }
            catch (Exception ex)
            {
                _logService.LogException(ex);
            }

            RaiseChanged();
        }

        private Dictionary<string, HashSet<string>> LoadSeen()
        {
            try
            {
                if (File.Exists(SeenFile))
                {
                    return JsonSerializer.Deserialize(File.ReadAllText(SeenFile), WatchedSearchesJson.Default.DictionaryStringHashSetString)
                           ?? new Dictionary<string, HashSet<string>>();
                }
            }
            catch (Exception ex)
            {
                // starting empty is safe: every search counts as new, so its videos are marked seen, not downloaded again
                Log("ERROR", "All searches", "Could not read " + SEEN_FILE + ", starting fresh: " + ex.Message);
            }

            return new Dictionary<string, HashSet<string>>();
        }

        // shortcut: the seen ids are never pruned (a few bytes per video), prune by date if the file ever gets big
        private void SaveSeen()
        {
            Directory.CreateDirectory(AppDataFolder);
            File.WriteAllText(SeenFile, JsonSerializer.Serialize(_seen, WatchedSearchesJson.Default.DictionaryStringHashSetString));
        }

        // One tab-separated line per event; ReadActivity parses it back for the Recent activity list.
        private void Log(string kind, string search, string message)
        {
            try
            {
                if (File.Exists(LogFile) && new FileInfo(LogFile).Length > LOG_MAX_BYTES)
                {
                    File.Delete(LogFile);
                }

                Directory.CreateDirectory(AppDataFolder);
                string line = string.Join("\t", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), kind, search,
                    message.Replace('\t', ' ').Replace('\n', ' ').Replace('\r', ' '));
                File.AppendAllText(LogFile, line + Environment.NewLine);
            }
            catch
            {
                // logging must never stop the loop
            }

            RaiseChanged();
        }

        #endregion Methods

        #region Nested Types

        internal class SearchesData
        {
            public bool Enabled { get; set; }

            public int Interval { get; set; } = DEFAULT_INTERVAL;

            public List<WatchedSearch> Searches { get; set; }
        }

        #endregion Nested Types
    }

    // Compile-time JSON code for the two files above; WriteIndented keeps searches.json readable.
    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(AutoDownloadService.SearchesData))]
    [JsonSerializable(typeof(Dictionary<string, HashSet<string>>))]
    internal partial class WatchedSearchesJson : JsonSerializerContext
    {
    }
}
