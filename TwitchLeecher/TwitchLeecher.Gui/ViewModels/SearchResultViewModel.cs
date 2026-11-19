using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Input;
using Avalonia;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using TwitchLeecher.Core.Enums;
using TwitchLeecher.Core.Events;
using TwitchLeecher.Core.Models;
using TwitchLeecher.Gui.Interfaces;
using TwitchLeecher.Gui.Types;
using TwitchLeecher.Services.Interfaces;
using TwitchLeecher.Shared.Commands;
using TwitchLeecher.Shared.Events;

namespace TwitchLeecher.Gui.ViewModels
{
    public partial class SearchResultViewModel : ViewModelBase, INavigationState
    {
        #region Fields

        private readonly IEventAggregator _eventAggregator;
        private readonly IApiService _apiService;
        private readonly IDialogService _dialogService;
        private readonly INavigationService _navigationService;
        private readonly IPreferencesService _preferencesService;
        private readonly ISearchService _searchService;
        private readonly IFilenameService _filenameService;
        private readonly IDownloadService _downloadService;
        private readonly INotificationService _notificationService;

        private readonly object _commandLockObject;

        private ICommand _viewCommand;
        private ICommand _downloadCommand;
        private ICommand _seachCommand;

        private bool _isAuthenticatedSubOnly;

        private bool _hideDownloaded;
        private int _sortIndex;
        private bool _isListView;
        private List<TwitchVideo> _displayed = new List<TwitchVideo>();
        private List<TwitchVideo> _hookedVideos = new List<TwitchVideo>();
        private ObservableCollection<TwitchVideo> _hookedCollection;

        // estimated bytes per video id; 0 = unknown, missing = not fetched yet
        private readonly Dictionary<string, long> _estimatedBytes = new Dictionary<string, long>();

        #endregion Fields

        #region Constructors

        public SearchResultViewModel(
            IEventAggregator eventAggregator,
            IApiService apiService,
            IAuthService authService,
            IDialogService dialogService,
            INavigationService navigationService,
            IPreferencesService preferencesService,
            ISearchService searchService,
            IFilenameService filenameService,
            IDownloadService downloadService,
            INotificationService notificationService)
        {
            _eventAggregator = eventAggregator;
            _apiService = apiService;
            _dialogService = dialogService;
            _navigationService = navigationService;
            _preferencesService = preferencesService;
            _searchService = searchService;
            _filenameService = filenameService;
            _downloadService = downloadService;
            _notificationService = notificationService;

            _searchService.PropertyChanged += SearchService_PropertyChanged;

            _commandLockObject = new object();

            _eventAggregator.GetEvent<SubOnlyAuthChangedEvent>().Subscribe(SubOnlyAuthChanged);

            _isAuthenticatedSubOnly = authService.IsAuthenticatedSubOnly;

            // queue changes may come from a background thread (e.g. automatic downloads)
            _downloadService.Downloads.CollectionChanged += (s, e) => Dispatcher.UIThread.Post(RefreshStatusesAndList);

            HookVideos();
        }

        #endregion Constructors

        #region Properties

        public Vector ScrollPosition { get; set; }

        public ObservableCollection<TwitchVideo> Videos
        {
            get { return _searchService.Videos; }
        }

        // Search summary bar: "somestreamer" + "Broadcasts · last 10 · 10 results"
        public string SearchTitle
        {
            get
            {
                SearchParameters p = _searchService.LastSearchParams;

                switch (p.SearchType)
                {
                    case SearchType.Urls: return "Video URLs";
                    case SearchType.Ids: return "Video IDs";
                    case SearchType.Favorites: return "Favourites";
                    default: return p.Channel;
                }
            }
        }

        public string SearchSummary
        {
            get
            {
                SearchParameters p = _searchService.LastSearchParams;
                List<string> parts = new List<string>();

                if (p.SearchType == SearchType.Channel || p.SearchType == SearchType.Favorites)
                {
                    parts.Add(p.VideoType switch
                    {
                        VideoType.Highlight => "Highlights",
                        VideoType.Upload => "Uploads",
                        VideoType.Clip => "Clips",
                        _ => "Broadcasts"
                    });

                    parts.Add(p.LoadLimitType switch
                    {
                        LoadLimitType.LastVods => "last " + p.LoadLastVods,
                        LoadLimitType.OnlyToday => "today",
                        _ => p.LoadFrom?.ToString("d MMM") + " – " + p.LoadTo?.ToString("d MMM")
                    });
                }

                int count = Videos?.Count ?? 0;
                string results = count == 1 ? "1 result" : count + " results";
                int hidden = count - _displayed.Count;

                parts.Add(hidden > 0 ? results + " (" + hidden + " hidden)" : results);

                return string.Join(" · ", parts);
            }
        }

        public bool HideDownloaded
        {
            get { return _hideDownloaded; }
            set
            {
                if (SetProperty(ref _hideDownloaded, value))
                {
                    RefreshDisplayed();
                }
            }
        }

        // 0 newest, 1 oldest, 2 longest, 3 most viewed (order of the dropdown items)
        public int SortIndex
        {
            get { return _sortIndex; }
            set
            {
                if (SetProperty(ref _sortIndex, value))
                {
                    RefreshDisplayed();
                }
            }
        }

        public bool IsListView
        {
            get { return _isListView; }
            set
            {
                if (SetProperty(ref _isListView, value))
                {
                    FirePropertyChanged(nameof(GridVideos));
                    FirePropertyChanged(nameof(ListVideos));
                }
            }
        }

        // only the visible layout gets items, so the hidden one never builds cards or loads thumbnails
        public List<TwitchVideo> GridVideos => _isListView ? null : _displayed;

        public List<TwitchVideo> ListVideos => _isListView ? _displayed : null;

        public bool AllSelected
        {
            get
            {
                List<TwitchVideo> selectable = _displayed.Where(v => !v.Live).ToList();
                return selectable.Count > 0 && selectable.All(v => v.IsSelected);
            }
            set
            {
                foreach (TwitchVideo video in _displayed.Where(v => !v.Live))
                {
                    video.IsSelected = value;
                }
            }
        }

        // same filter as DownloadSelected, so the count matches what the button queues
        private List<TwitchVideo> Selected => Videos?.Where(v => v.IsSelected && !v.Live).ToList() ?? new List<TwitchVideo>();

        public bool HasSelection => Selected.Count > 0;

        public string SelectionSummary
        {
            get
            {
                List<TwitchVideo> selected = Selected;
                string summary = selected.Count + " selected";
                long known = selected.Sum(v => _estimatedBytes.TryGetValue(v.Id, out long b) ? b : 0);

                if (known <= 0)
                {
                    return summary;
                }

                bool allKnown = selected.All(v => _estimatedBytes.TryGetValue(v.Id, out long b) && b > 0);
                double gb = known / 1024.0 / 1024.0 / 1024.0;
                string size = gb >= 1 ? gb.ToString("0.0") + " GB" : (known / 1024.0 / 1024.0).ToString("0") + " MB";

                return summary + " · ~" + size + (allKnown ? "" : " + unknown");
            }
        }

        public string DownloadSelectedLabel
        {
            get
            {
                int count = Selected.Count;
                return count == 1 ? "Download 1 video" : "Download " + count + " videos";
            }
        }

        public ICommand ViewCommand
        {
            get
            {
                if (_viewCommand == null)
                {
                    _viewCommand = new DelegateCommand<string>(ViewVideo);
                }

                return _viewCommand;
            }
        }

        public ICommand DownloadCommand
        {
            get
            {
                if (_downloadCommand == null)
                {
                    _downloadCommand = new DelegateCommand<string>(DownloadVideo);
                }

                return _downloadCommand;
            }
        }

        public ICommand SeachCommnad
        {
            get
            {
                if (_seachCommand == null)
                {
                    _seachCommand = new DelegateCommand(ShowSearch);
                }

                return _seachCommand;
            }
        }

        #endregion Properties

        #region Methods

        private void SubOnlyAuthChanged(bool isAuthenticatedSubOnly)
        {
            _isAuthenticatedSubOnly = isAuthenticatedSubOnly;
        }

        private void ViewVideo(string id)
        {
            try
            {
                lock (_commandLockObject)
                {
                    if (!string.IsNullOrWhiteSpace(id))
                    {
                        TwitchVideo video = Videos.Where(v => v.Id == id).FirstOrDefault();

                        if (video != null && video.Url != null && video.Url.IsAbsoluteUri)
                        {
                            StartVideoStream(video);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowAndLogException(ex);
            }
        }

        private void StartVideoStream(TwitchVideo video)
        {
            try
            {
                Preferences currentPrefs = _preferencesService.CurrentPreferences;

                if (currentPrefs.MiscUseExternalPlayer)
                {
                    Process.Start(currentPrefs.MiscExternalPlayer, video.Url.ToString());
                }
                else
                {
                    // Do not execute any string as command
                    if (!video.Url.ToString().StartsWith("https://")) {
                        throw new Exception("Unsupported video URL");
                    }
                    
                    Process.Start(new ProcessStartInfo(video.Url.ToString()) {
                        UseShellExecute = true,
                        Verb = "open"
                    });
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowAndLogException(ex);
            }
        }

        private void DownloadVideo(string id)
        {
            try
            {
                lock (_commandLockObject)
                {
                    if (!string.IsNullOrWhiteSpace(id))
                    {
                        TwitchVideo video = Videos.Where(v => v.Id == id).FirstOrDefault();

                        if (video != null)
                        {
                            DownloadParameters downloadParams = CreateDownloadParams(video, out string problem);

                            if (downloadParams == null)
                            {
                                _dialogService.ShowMessageBox(problem, "Download", MessageBoxButton.OK,
                                    MessageBoxImage.Exclamation);
                                return;
                            }

                            if (_preferencesService.CurrentPreferences.DownloadQuickDownload)
                            {
                                // #96: skip the dialog and queue with the default settings
                                string notQueued = TryEnqueue(downloadParams);
                                _notificationService.ShowNotification(notQueued ?? "Download added");
                            }
                            else
                            {
                                _navigationService.ShowDownload(downloadParams);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowAndLogException(ex);
            }
        }

        // Network calls only, no UI: also runs on a background thread for "Download Selected".
        private DownloadParameters CreateDownloadParams(TwitchVideo video, out string problem)
        {
            Dictionary<TwitchVideoQuality, string> playlistInfo;

            if (video.IsClip)
            {
                playlistInfo = _apiService.GetClipQualities(video.Id);
            }
            else
            {
                TwitchVideoAuthInfo vodAuthInfo = _apiService.GetVodAuthInfo(video.Id);

                if (!vodAuthInfo.Privileged && vodAuthInfo.SubOnly)
                {
                    problem = _isAuthenticatedSubOnly
                        ? "This video is sub-only but you are not subscribed to the channel!"
                        : "This video is sub-only! You need to enable sub-only video download support first!";
                    return null;
                }

                playlistInfo = _apiService.GetPlaylistInfo(video.Id, vodAuthInfo);
            }

            List<TwitchVideoQuality> qualities = playlistInfo.Keys.OrderBy(q => q).ToList();

            Preferences currentPrefs = _preferencesService.CurrentPreferences.Clone();

            TwitchVideoQuality selectedQuality = GetSelectedQuality(qualities, currentPrefs.DownloadDefaultQuality);

            (string folder, string filename, bool disableConversion) = GetDefaultTarget(video, currentPrefs, selectedQuality);

            DownloadParameters downloadParams = new DownloadParameters(video, qualities,
                selectedQuality, folder, filename, disableConversion);

            if (video.StartTime.HasValue)
            {
                downloadParams.CropStartTime = video.StartTime.Value;
            }

            problem = null;
            return downloadParams;
        }

        // Where quick download / Download Selected would save this video
        private (string folder, string filename, bool disableConversion) GetDefaultTarget(TwitchVideo video,
            Preferences prefs, TwitchVideoQuality quality)
        {
            string folder = prefs.DownloadSubfoldersForFav &&
                            _preferencesService.IsChannelInFavourites(video.Channel)
                ? Path.Combine(prefs.DownloadFolder, video.Channel)
                : prefs.DownloadFolder;

            // clips are always a ready-made mp4
            bool disableConversion = prefs.DownloadDisableConversion && !video.IsClip;

            string filename = _filenameService.SubstituteWildcards(prefs.DownloadFileName, video, quality);
            filename = _filenameService.EnsureExtension(filename, disableConversion);

            return (folder, filename, disableConversion);
        }

        private void RefreshStatusesAndList()
        {
            // only rebuild the cards when the hidden set changed, a rebuild loses the scroll position
            if (RefreshStatuses() && _hideDownloaded)
            {
                RefreshDisplayed();
            }
        }

        // Sets the QUEUED and DOWNLOADED badges; true when any DOWNLOADED badge changed
        private bool RefreshStatuses()
        {
            bool downloadedChanged = false;

            try
            {
                Preferences prefs = _preferencesService.CurrentPreferences;

                HashSet<string> queuedIds = _downloadService.Downloads
                    .Where(d => d.DownloadState == DownloadState.Queued || d.DownloadState == DownloadState.Paused ||
                                d.DownloadState == DownloadState.Downloading)
                    .Select(d => d.DownloadParams.Video.Id)
                    .ToHashSet();

                // shortcut: only finds files saved under the default folder/filename, and not at all when the
                // filename uses {res} (the quality is unknown until the playlist is fetched); upgrade by keeping
                // a record of finished downloads by video id if renamed files should count too
                bool canGuessPath = !string.IsNullOrWhiteSpace(prefs.DownloadFileName) &&
                                    !prefs.DownloadFileName.Contains(FilenameWildcards.RES);
                TwitchVideoQuality anyQuality = new TwitchVideoQuality("chunked", "Source");

                foreach (TwitchVideo video in Videos ?? new ObservableCollection<TwitchVideo>())
                {
                    string path = null;

                    if (canGuessPath)
                    {
                        (string folder, string filename, _) = GetDefaultTarget(video, prefs, anyQuality);
                        path = Path.Combine(folder, filename);
                    }

                    bool downloaded = path != null && File.Exists(path);
                    downloadedChanged |= downloaded != video.IsDownloaded;
                    video.IsDownloaded = downloaded;
                    video.IsQueued = !video.IsDownloaded &&
                                     (queuedIds.Contains(video.Id) || (path != null && _downloadService.IsFileNameUsed(path)));
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowAndLogException(ex);
            }

            return downloadedChanged;
        }

        private void RefreshDisplayed()
        {
            IEnumerable<TwitchVideo> videos = Videos ?? Enumerable.Empty<TwitchVideo>();

            if (_hideDownloaded)
            {
                videos = videos.Where(v => !v.IsDownloaded);
            }

            videos = _sortIndex switch
            {
                1 => videos.OrderBy(v => v.RecordedDate),
                2 => videos.OrderByDescending(v => v.Length),
                3 => videos.OrderByDescending(v => v.Views),
                _ => videos.OrderByDescending(v => v.RecordedDate)
            };

            _displayed = videos.ToList();

            FirePropertyChanged(nameof(GridVideos));
            FirePropertyChanged(nameof(ListVideos));
            FirePropertyChanged(nameof(SearchTitle));
            FirePropertyChanged(nameof(SearchSummary));
            FireSelectionChanged();
        }

        private void FireSelectionChanged()
        {
            FirePropertyChanged(nameof(AllSelected));
            FirePropertyChanged(nameof(HasSelection));
            FirePropertyChanged(nameof(SelectionSummary));
            FirePropertyChanged(nameof(DownloadSelectedLabel));
        }

        // Follows ticks on every result, for the footer and the size estimate
        private void HookVideos()
        {
            foreach (TwitchVideo video in _hookedVideos)
            {
                video.PropertyChanged -= Video_PropertyChanged;
            }

            if (_hookedCollection != null)
            {
                _hookedCollection.CollectionChanged -= Videos_CollectionChanged;
            }

            _hookedCollection = Videos;
            _hookedVideos = Videos?.ToList() ?? new List<TwitchVideo>();

            foreach (TwitchVideo video in _hookedVideos)
            {
                video.PropertyChanged += Video_PropertyChanged;
            }

            if (_hookedCollection != null)
            {
                _hookedCollection.CollectionChanged += Videos_CollectionChanged;
            }

            RefreshStatuses();
            RefreshDisplayed();
        }

        // shortcut: fetches the playlist (2 API calls) for every ticked video to learn its bitrate, so "Select all"
        // on a big search makes many requests; upgrade by estimating from a fixed bitrate per default quality
        private void EstimateSize(TwitchVideo video)
        {
            if (_estimatedBytes.ContainsKey(video.Id))
            {
                return;
            }

            _estimatedBytes[video.Id] = 0;

            Task.Run(() =>
            {
                DownloadParameters p = CreateDownloadParams(video, out _);
                long bandwidth = p?.SelectedQuality?.Bandwidth ?? 0;
                return (long)(bandwidth / 8.0 * video.Length.TotalSeconds);
            }).ContinueWith(task =>
            {
                // failures (sub-only, network) just stay unknown
                _estimatedBytes[video.Id] = task.IsCompletedSuccessfully ? task.Result : 0;
                FirePropertyChanged(nameof(SelectionSummary));
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        public override void OnBeforeShown()
        {
            base.OnBeforeShown();
            RefreshStatusesAndList();
        }

        // Same checks as the download dialog, minus the questions: never overwrites. Returns why it was skipped, or null.
        private string TryEnqueue(DownloadParameters downloadParams)
        {
            downloadParams.Validate();

            if (downloadParams.HasErrors)
            {
                return "Invalid download settings, check Settings > Download";
            }

            if (_downloadService.IsFileNameUsed(downloadParams.FullPath))
            {
                return "Already in the download queue";
            }

            if (File.Exists(downloadParams.FullPath))
            {
                return "File already exists";
            }

            _downloadService.Enqueue(downloadParams);
            return null;
        }

        [RelayCommand]
        private void ShowGrid() => IsListView = false;

        [RelayCommand]
        private void ShowList() => IsListView = true;

        [RelayCommand]
        private async Task DownloadSelected()
        {
            try
            {
                List<TwitchVideo> selected = Videos.Where(v => v.IsSelected && !v.Live).ToList();

                if (selected.Count == 0)
                {
                    _notificationService.ShowNotification("Tick the videos you want first");
                    return;
                }

                _dialogService.SetBusy();

                int added = 0;
                List<string> skipped = new List<string>();

                foreach (TwitchVideo video in selected)
                {
                    // API calls off the UI thread so the window stays responsive for long selections
                    (DownloadParameters downloadParams, string problem) = await Task.Run(() =>
                    {
                        DownloadParameters p = CreateDownloadParams(video, out string reason);
                        return (p, reason);
                    });

                    string notQueued = downloadParams == null ? problem : TryEnqueue(downloadParams);

                    if (notQueued == null)
                    {
                        added++;
                        video.IsSelected = false;
                    }
                    else
                    {
                        skipped.Add(video.Title + ": " + notQueued);
                    }
                }

                if (skipped.Count == 0)
                {
                    _notificationService.ShowNotification(added + " download(s) added");
                }
                else
                {
                    await _dialogService.ShowMessageBox(
                        added + " download(s) added. Skipped (still ticked):" + Environment.NewLine +
                        string.Join(Environment.NewLine, skipped),
                        "Download Selected", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowAndLogException(ex);
            }
        }

        private TwitchVideoQuality GetSelectedQuality(List<TwitchVideoQuality> qualities, DefaultQuality defaultQuality)
        {
            TwitchVideoQuality sourceQuality = qualities.Find(q => q.IsSource);

            if (sourceQuality == null)
            {
                sourceQuality = qualities[0];
            }

            if (defaultQuality.IsSource)
            {
                return sourceQuality;
            }

            if (defaultQuality.IsAudioOnly)
            {
                TwitchVideoQuality audioOnlyQuality = qualities.FirstOrDefault(q => q.IsAudioOnly);

                if (audioOnlyQuality != null)
                {
                    return audioOnlyQuality;
                }
                else
                {
                    return sourceQuality;
                }
            }

            IEnumerable<TwitchVideoQuality> visualQualities = qualities.Where(q => !q.IsAudioOnly);

            int defaultRes = defaultQuality.VerticalResolution;

            TwitchVideoQuality selectedQuality = null;

            foreach (TwitchVideoQuality quality in visualQualities)
            {
                if (quality.VerticalResolution <= defaultRes && (selectedQuality == null ||
                                                                 selectedQuality.VerticalResolution <
                                                                 quality.VerticalResolution))
                {
                    selectedQuality = quality;
                }
            }

            if (selectedQuality != null)
            {
                return selectedQuality;
            }

            foreach (TwitchVideoQuality quality in visualQualities)
            {
                if (quality.VerticalResolution >= defaultRes && (selectedQuality == null ||
                                                                 selectedQuality.VerticalResolution >
                                                                 quality.VerticalResolution))
                {
                    selectedQuality = quality;
                }
            }

            if (selectedQuality != null)
            {
                return selectedQuality;
            }

            return sourceQuality;
        }

        public void ShowSearch()
        {
            try
            {
                lock (_commandLockObject)
                {
                    _navigationService.ShowSearch();
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowAndLogException(ex);
            }
        }

        protected override List<MenuCommand> BuildMenu()
        {
            List<MenuCommand> menuCommands = base.BuildMenu();

            if (menuCommands == null)
            {
                menuCommands = new List<MenuCommand>();
            }

            menuCommands.Add(new MenuCommand(SeachCommnad, "New Search", "fa-solid fa-magnifying-glass"));
            menuCommands.Add(new MenuCommand(DownloadSelectedCommand, "Download Selected", "fa-solid fa-list-check", 190));

            return menuCommands;
        }

        #endregion Methods

        #region EventHandlers

        private void SearchService_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            string propertyName = e.PropertyName;

            FirePropertyChanged(propertyName);

            if (propertyName.Equals(nameof(Videos)))
            {
                ScrollPosition = Vector.Zero;
                HookVideos();
            }
        }

        private void Videos_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            HookVideos();
        }

        private void Video_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(TwitchVideo.IsSelected))
            {
                TwitchVideo video = (TwitchVideo)sender;

                if (video.IsSelected && !video.Live)
                {
                    EstimateSize(video);
                }

                FireSelectionChanged();
            }
        }

        #endregion EventHandlers
    }
}