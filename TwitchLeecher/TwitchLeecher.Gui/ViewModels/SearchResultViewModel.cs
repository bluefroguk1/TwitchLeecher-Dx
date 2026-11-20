using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Input;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using TwitchLeecher.Core;
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
        private readonly IDialogService _dialogService;
        private readonly INavigationService _navigationService;
        private readonly IPreferencesService _preferencesService;
        private readonly ISearchService _searchService;
        private readonly IApiService _apiService;
        private readonly IQuickDownloadService _quickDownloadService;
        private readonly IDownloadService _downloadService;
        private readonly INotificationService _notificationService;
        private readonly IAutoDownloadService _autoDownloadService;

        private readonly object _commandLockObject;

        private ICommand _viewCommand;
        private ICommand _downloadCommand;
        private ICommand _seachCommand;


        // the search these results came from; SearchService resets its LastSearchParams when preferences are saved
        private SearchParameters _resultParams;
        private string _channelDisplayName;
        private Bitmap _channelAvatar;

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
            IDialogService dialogService,
            INavigationService navigationService,
            IPreferencesService preferencesService,
            ISearchService searchService,
            IApiService apiService,
            IQuickDownloadService quickDownloadService,
            IDownloadService downloadService,
            INotificationService notificationService,
            IAutoDownloadService autoDownloadService)
        {
            _autoDownloadService = autoDownloadService;
            _eventAggregator = eventAggregator;
            _dialogService = dialogService;
            _navigationService = navigationService;
            _preferencesService = preferencesService;
            _searchService = searchService;
            _apiService = apiService;
            _quickDownloadService = quickDownloadService;
            _downloadService = downloadService;
            _notificationService = notificationService;

            _searchService.PropertyChanged += SearchService_PropertyChanged;

            _commandLockObject = new object();

            // favourites may be edited in Settings while this page is kept alive
            _eventAggregator.GetEvent<PreferencesSavedEvent>().Subscribe(() => FirePropertyChanged(nameof(IsFavourite)));

            _resultParams = _searchService.LastSearchParams;
            LoadChannelHeader();

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
                SearchParameters p = _resultParams;

                switch (p.SearchType)
                {
                    case SearchType.Urls: return "Video URLs";
                    case SearchType.Ids: return "Video IDs";
                    case SearchType.Favorites: return "Favourites";
                    default: return _channelDisplayName ?? p.Channel;
                }
            }
        }

        // Channel header: avatar and favourite star, channel searches only
        public bool IsChannelSearch => _resultParams.SearchType == SearchType.Channel;

        public Bitmap ChannelAvatar
        {
            get => _channelAvatar;
            private set => SetProperty(ref _channelAvatar, value, nameof(ChannelAvatar));
        }

        public bool IsFavourite =>
            IsChannelSearch
            && _preferencesService.CurrentPreferences.SearchFavouriteChannels.Any(c =>
                c.Equals(_resultParams.Channel, StringComparison.OrdinalIgnoreCase));

        public string SearchSummary
        {
            get
            {
                SearchParameters p = _resultParams;
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
                            DownloadParameters downloadParams = _quickDownloadService.CreateDownloadParams(video, out string problem);

                            if (downloadParams == null)
                            {
                                _dialogService.ShowMessageBox(problem, "Download", MessageBoxButton.OK,
                                    MessageBoxImage.Exclamation);
                                return;
                            }

                            if (_preferencesService.CurrentPreferences.DownloadQuickDownload)
                            {
                                // #96: skip the dialog and queue with the default settings
                                string notQueued = _quickDownloadService.TryEnqueue(downloadParams);
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
                        path = _quickDownloadService.GetDefaultPath(video, anyQuality);
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

        private async void LoadChannelHeader()
        {
            SearchParameters p = _resultParams;

            _channelDisplayName = null;
            ChannelAvatar = null;
            FirePropertyChanged(nameof(IsChannelSearch));
            FirePropertyChanged(nameof(IsFavourite));

            if (p.SearchType != SearchType.Channel || string.IsNullOrWhiteSpace(p.Channel))
            {
                return;
            }

            try
            {
                var info = await Task.Run(() => _apiService.GetChannelInfo(p.Channel));

                // a newer search may have finished meanwhile
                if (info == null || p != _resultParams)
                {
                    return;
                }

                _channelDisplayName = info.Value.DisplayName;
                FirePropertyChanged(nameof(SearchTitle));

                if (Uri.TryCreate(info.Value.ProfileImageUrl, UriKind.Absolute, out Uri avatarUrl))
                {
                    Bitmap avatar = await ImageHelper.LoadFromWeb(avatarUrl);

                    if (p == _resultParams)
                    {
                        ChannelAvatar = avatar;
                    }
                }
            }
            catch (Exception ex)
            {
                // the header is cosmetic, the results stay usable without it
                Console.WriteLine($"Could not load channel header for '{p.Channel}': {ex.Message}");
            }
        }

        [RelayCommand]
        private void ToggleFavourite()
        {
            try
            {
                lock (_commandLockObject)
                {
                    Preferences prefs = _preferencesService.CurrentPreferences;
                    string existing = prefs.SearchFavouriteChannels.FirstOrDefault(c =>
                        c.Equals(_resultParams.Channel, StringComparison.OrdinalIgnoreCase));

                    if (existing != null)
                    {
                        prefs.SearchFavouriteChannels.Remove(existing);
                    }
                    else
                    {
                        prefs.SearchFavouriteChannels.Add(_resultParams.Channel);
                    }

                    _preferencesService.Save(prefs);
                    _notificationService.ShowNotification(existing != null ? "Removed from favourites" : "Added to favourites");
                    FirePropertyChanged(nameof(IsFavourite));
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowAndLogException(ex);
            }
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
                DownloadParameters p = _quickDownloadService.CreateDownloadParams(video, out _);
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

        [RelayCommand]
        private void ShowGrid() => IsListView = false;

        [RelayCommand]
        private void ShowList() => IsListView = true;

        // Saves the current channel search as a watched search with default settings.
        [RelayCommand]
        private void WatchSearch()
        {
            try
            {
                SearchParameters searchParams = _searchService.LastSearchParams;

                if (searchParams == null || searchParams.SearchType != SearchType.Channel ||
                    string.IsNullOrWhiteSpace(searchParams.Channel))
                {
                    _notificationService.ShowNotification("Only a channel search can be watched");
                    return;
                }

                bool wasOn = _autoDownloadService.Enabled;
                WatchedSearch search = _autoDownloadService.Add(searchParams.Channel, searchParams.VideoType);

                // asking to watch a search means wanting it to run
                _autoDownloadService.Enabled = true;

                _notificationService.ShowNotification("Watching " + search.Label +
                    (wasOn ? "" : ", watched searches switched on") + ". Change it in Settings");
            }
            catch (Exception ex)
            {
                _dialogService.ShowAndLogException(ex);
            }
        }

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
                        DownloadParameters p = _quickDownloadService.CreateDownloadParams(video, out string reason);
                        return (p, reason);
                    });

                    string notQueued = downloadParams == null ? problem : _quickDownloadService.TryEnqueue(downloadParams);

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
            menuCommands.Add(new MenuCommand(WatchSearchCommand, "Watch this search", "fa-solid fa-eye", 190));

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
                _resultParams = _searchService.LastSearchParams;
                LoadChannelHeader();
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