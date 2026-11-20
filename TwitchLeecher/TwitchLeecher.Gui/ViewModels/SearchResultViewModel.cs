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
using TwitchLeecher.Gui.ViewModels.Results;
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
        private readonly IQuickDownloadService _quickDownloadService;
        private readonly IDownloadService _downloadService;
        private readonly INotificationService _notificationService;
        private readonly IAutoDownloadService _autoDownloadService;

        private readonly object _commandLockObject;

        private ICommand _viewCommand;
        private ICommand _downloadCommand;



        private readonly ChannelHeader _header;
        private readonly ResultStatusTracker _statusTracker;
        private readonly ResultSelection _selection;

        private bool _hideDownloaded;
        private int _sortIndex;
        private bool _isListView;
        private List<TwitchVideo> _displayed = new List<TwitchVideo>();
        private List<TwitchVideo> _hookedVideos = new List<TwitchVideo>();
        private ObservableCollection<TwitchVideo> _hookedCollection;

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
            _quickDownloadService = quickDownloadService;
            _downloadService = downloadService;
            _notificationService = notificationService;

            _searchService.PropertyChanged += SearchService_PropertyChanged;

            _commandLockObject = new object();

            // favourites may be edited in Settings while this page is kept alive
            _eventAggregator.GetEvent<PreferencesSavedEvent>().Subscribe(() => FirePropertyChanged(nameof(IsFavourite)));

            _header = new ChannelHeader(apiService, _preferencesService, _notificationService, _dialogService, FirePropertyChanged);
            _selection = new ResultSelection(_quickDownloadService, () => Videos, () => FirePropertyChanged(nameof(SelectionSummary)));
            _statusTracker = new ResultStatusTracker(_eventAggregator, _preferencesService, _downloadService,
                _quickDownloadService, _dialogService, () => Videos);

            // a finished download changes state without leaving the list; only rebuild the cards when the
            // hidden set changed, a rebuild loses the scroll position
            _statusTracker.Refreshed += downloadedChanged =>
            {
                if (downloadedChanged && _hideDownloaded)
                {
                    RefreshDisplayed();
                }
            };

            _header.Load(_searchService.LastSearchParams);

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
                SearchParameters p = _header.Params;

                switch (p.SearchType)
                {
                    case SearchType.Urls: return "Video URLs";
                    case SearchType.Ids: return "Video IDs";
                    case SearchType.Favorites: return "Favourites";
                    default: return _header.DisplayName ?? p.Channel;
                }
            }
        }

        // Channel header: avatar and favourite star, channel searches only
        public bool IsChannelSearch => _header.IsChannelSearch;

        public Bitmap ChannelAvatar => _header.Avatar;

        public bool IsFavourite => _header.IsFavourite;

        public string SearchSummary
        {
            get
            {
                SearchParameters p = _header.Params;
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

        public bool HasSelection => _selection.HasSelection;

        public string SelectionSummary => _selection.Summary;

        public string DownloadSelectedLabel
        {
            get
            {
                int count = _selection.Selected.Count;
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
                                if (notQueued != null)
                                {
                                    _notificationService.ShowNotification(notQueued);
                                }
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

        [RelayCommand]
        private void ToggleFavourite() => _header.ToggleFavourite();

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
            _statusTracker.ClearPathCache();

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

            _statusTracker.Refresh();
            RefreshDisplayed();
        }

        public override void OnBeforeShown()
        {
            base.OnBeforeShown();
            // only rebuild the cards when the hidden set changed, a rebuild loses the scroll position
            if (_statusTracker.Refresh() && _hideDownloaded)
            {
                RefreshDisplayed();
            }
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
                List<TwitchVideo> selected = _selection.Selected;

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

        #endregion Methods

        #region EventHandlers

        private void SearchService_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            string propertyName = e.PropertyName;

            FirePropertyChanged(propertyName);

            if (propertyName.Equals(nameof(Videos)))
            {
                _header.Load(_searchService.LastSearchParams);
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
                    _selection.EstimateSize(video);
                }

                FireSelectionChanged();
            }
        }

        #endregion EventHandlers
    }
}