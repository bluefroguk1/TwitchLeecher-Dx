using System.ComponentModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using TwitchLeecher.Core.Enums;
using TwitchLeecher.Core.Models;
using TwitchLeecher.Gui.Interfaces;
using TwitchLeecher.Services.Interfaces;
using TwitchLeecher.Shared.Commands;
using TwitchLeecher.Shared.Events;
using TwitchLeecher.Shared.Extensions;
using TwitchLeecher.Shared.Helpers;
using TwitchLeecher.Shared.Reflection;

namespace TwitchLeecher.Gui.ViewModels
{
    public record RecentSearchItem(string Label, SearchParameters Params);

    public partial class SearchViewModel : ViewModelBase
    {
        #region Fields

        // Indexes of the "Show" dropdown in SearchView.axaml
        private const int SHOW_LAST_VODS = 0;
        private const int SHOW_TODAY = 1;
        private const int SHOW_LAST_7_DAYS = 2;
        private const int SHOW_LAST_30_DAYS = 3;
        private const int SHOW_CUSTOM = 4;

        private SearchParameters _searchParams;
        private bool _isLinksMode;
        private string _linksText;
        private int _showIndex;

        private ICommand _cancelCommand;

        private readonly IApiService _apiService;
        private readonly ISearchService _searchService;
        private readonly IDialogService _dialogService;
        private readonly INavigationService _navigationService;
        private readonly IPreferencesService _preferencesService;
        private readonly IEventAggregator _eventAggregator;
        private readonly INotificationService _notificationService;
        private readonly IRuntimeDataService _runtimeDataService;
        private List<RecentSearchItem> _recentSearches;

        private readonly object _commandLockObject;
        private bool _isDropdownOpen;

        #endregion Fields

        #region Constructors

        public SearchViewModel(
            IApiService apiService,
            ISearchService searchService,
            IDialogService dialogService,
            INavigationService navigationService,
            IPreferencesService preferencesService,
            IRuntimeDataService runtimeDataService,
            IEventAggregator eventAggregator,
            INotificationService notificationService,
            SearchResultViewModel results
        )
        {
            _apiService = apiService;
            _searchService = searchService;
            _dialogService = dialogService;
            _navigationService = navigationService;
            _preferencesService = preferencesService;
            _eventAggregator = eventAggregator;
            _notificationService = notificationService;
            _runtimeDataService = runtimeDataService;
            Results = results;

            _commandLockObject = new object();

            AssemblyUtil au = AssemblyUtil.Get;
            ProductTitle = au.GetProductName() + " " + au.GetAssemblyVersion().Trim();

            LoadRecentSearches();

            _searchParams = _searchService.LastSearchParams.Clone();
            LoadForm();
        }

        #endregion Constructors

        #region Properties

        public SearchParameters SearchParams
        {
            get { return _searchParams; }
            set { SetProperty(ref _searchParams, value, nameof(SearchParams)); }
        }

        public string ProductTitle { get; }

        public List<RecentSearchItem> RecentSearches => _recentSearches;

        public bool HasRecentSearches => _recentSearches.Count > 0;

        // The results are shown in the right-hand pane next to the sidebar, so a search never leaves this page.
        public SearchResultViewModel Results { get; }

        public bool IsSearching => _searchService.IsSearching;

        public bool ShowResults => !IsSearching && _searchService.HasSearched && (_searchService.Videos?.Count ?? 0) > 0;

        public bool ShowNoResults => !IsSearching && _searchService.HasSearched && (_searchService.Videos?.Count ?? 0) == 0;

        public bool ShowEmptyState => !IsSearching && !_searchService.HasSearched;

        // placeholder cards while a search is loading
        public int[] SkeletonCards { get; } = new int[8];

        public RangeObservableCollection<string> FavChannels
        {
            get { return _preferencesService.CurrentPreferences.SearchFavouriteChannels; }
        }

        public bool HasFavChannels => FavChannels.Count > 0;

        public bool IsLinksMode
        {
            get => _isLinksMode;
            set => SetProperty(ref _isLinksMode, value);
        }

        public string LinksText
        {
            get => _linksText;
            set => SetProperty(ref _linksText, value);
        }

        public int ShowIndex
        {
            get => _showIndex;
            set
            {
                if (!SetProperty(ref _showIndex, value))
                {
                    return;
                }

                DateTime today = DateTime.Now.Date;

                switch (value)
                {
                    case SHOW_LAST_VODS:
                        SearchParams.LoadLimitType = LoadLimitType.LastVods;
                        break;

                    case SHOW_TODAY:
                        SearchParams.LoadLimitType = LoadLimitType.OnlyToday;
                        SearchParams.LoadFrom = today;
                        SearchParams.LoadTo = today;
                        break;

                    case SHOW_LAST_7_DAYS:
                        SearchParams.LoadLimitType = LoadLimitType.Timespan;
                        SearchParams.LoadFrom = today.AddDays(-7);
                        SearchParams.LoadTo = today;
                        break;

                    case SHOW_LAST_30_DAYS:
                        SearchParams.LoadLimitType = LoadLimitType.Timespan;
                        SearchParams.LoadFrom = today.AddDays(-30);
                        SearchParams.LoadTo = today;
                        break;

                    default:
                        SearchParams.LoadLimitType = LoadLimitType.Timespan;
                        break;
                }

                FireShowChanged();
            }
        }

        public bool IsLastVods => _showIndex == SHOW_LAST_VODS;

        public bool IsCustomRange => _showIndex == SHOW_CUSTOM;

        // One bool per pill in the sidebar (radio buttons set theirs to true to choose it)
        public bool ShowLastVodsPill { get => _showIndex == SHOW_LAST_VODS; set { if (value) ShowIndex = SHOW_LAST_VODS; } }
        public bool ShowTodayPill { get => _showIndex == SHOW_TODAY; set { if (value) ShowIndex = SHOW_TODAY; } }
        public bool Show7DaysPill { get => _showIndex == SHOW_LAST_7_DAYS; set { if (value) ShowIndex = SHOW_LAST_7_DAYS; } }
        public bool Show30DaysPill { get => _showIndex == SHOW_LAST_30_DAYS; set { if (value) ShowIndex = SHOW_LAST_30_DAYS; } }
        public bool ShowCustomPill { get => _showIndex == SHOW_CUSTOM; set { if (value) ShowIndex = SHOW_CUSTOM; } }

        private void FireShowChanged()
        {
            FirePropertyChanged(nameof(ShowIndex));
            FirePropertyChanged(nameof(IsLastVods));
            FirePropertyChanged(nameof(IsCustomRange));
            FirePropertyChanged(nameof(ShowLastVodsPill));
            FirePropertyChanged(nameof(ShowTodayPill));
            FirePropertyChanged(nameof(Show7DaysPill));
            FirePropertyChanged(nameof(Show30DaysPill));
            FirePropertyChanged(nameof(ShowCustomPill));
        }

        public ICommand CancelCommand
        {
            get
            {
                if (_cancelCommand == null)
                {
                    _cancelCommand = new DelegateCommand(Cancel);
                }

                return _cancelCommand;
            }
        }

        public bool IsDropdownOpen
        {
            get => _isDropdownOpen;
            set => SetProperty(ref _isDropdownOpen, value);
        }

        #endregion Properties

        #region Methods

        // Puts SearchParams into the sidebar's own controls (source switch, links box, "Show" dropdown)
        private void LoadForm()
        {
            DateTime today = DateTime.Now.Date;

            IsLinksMode = SearchParams.SearchType is SearchType.Urls or SearchType.Ids;
            LinksText = SearchParams.SearchType == SearchType.Ids ? SearchParams.Ids : SearchParams.Urls;

            _showIndex = SearchParams.LoadLimitType switch
            {
                LoadLimitType.LastVods => SHOW_LAST_VODS,
                LoadLimitType.OnlyToday => SHOW_TODAY,
                _ when SearchParams.LoadFrom == today.AddDays(-7) && SearchParams.LoadTo == today => SHOW_LAST_7_DAYS,
                _ when SearchParams.LoadFrom == today.AddDays(-30) && SearchParams.LoadTo == today => SHOW_LAST_30_DAYS,
                _ => SHOW_CUSTOM
            };

            FireShowChanged();
        }

        // One box for links and IDs: all IDs -> ID search, otherwise IDs become video links -> URL search
        private void ApplyLinks()
        {
            string[] lines = (LinksText ?? string.Empty)
                .ReplaceLineEndings("\n")
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            bool allIds = lines.Length > 0 && lines.All(IsId);

            if (allIds)
            {
                SearchParams.SearchType = SearchType.Ids;
                SearchParams.Ids = string.Join(Environment.NewLine, lines);
                SearchParams.Urls = null;
            }
            else
            {
                SearchParams.SearchType = SearchType.Urls;
                SearchParams.Urls = string.Join(
                    Environment.NewLine,
                    lines.Select(l => IsId(l) ? "https://www.twitch.tv/videos/" + l : l)
                );
                SearchParams.Ids = null;
            }
        }

        private static bool IsId(string line) => ulong.TryParse(line, out ulong id) && id > 0;

        private static string Describe(SearchParameters s)
        {
            string type = s.VideoType switch
            {
                VideoType.Highlight => "Highlights",
                VideoType.Upload => "Uploads",
                _ => "Broadcasts"
            };

            return s.SearchType switch
            {
                SearchType.Channel => s.Channel + " \u00B7 " + type,
                SearchType.Favorites => "\u2605 Favourites \u00B7 " + type,
                SearchType.Urls => CountLines(s.Urls) + " link(s)",
                _ => CountLines(s.Ids) + " ID(s)"
            };
        }

        private static int CountLines(string text) =>
            (text ?? string.Empty).Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).Length;

        [RelayCommand]
        private void Search()
        {
            if (IsLinksMode)
            {
                ApplyLinks();
            }
            else
            {
                SearchParams.SearchType = SearchType.Channel;
            }

            RunSearch();
        }

        [RelayCommand]
        private void SearchFavourites()
        {
            SearchParams.SearchType = SearchType.Favorites;

            // shortcut: favourites search is date-range only (ApiService.SearchFavorites), so "Last N videos"
            // uses the default date range from Settings; add a per-channel count if users ask for it
            if (SearchParams.LoadLimitType == LoadLimitType.OnlyToday)
            {
                SearchParams.LoadFrom = DateTime.Now.Date;
                SearchParams.LoadTo = DateTime.Now.Date;
            }

            RunSearch();
        }

        [RelayCommand]
        private void PickFavourite(string channel)
        {
            IsLinksMode = false;
            SearchParams.Channel = channel;
            Search();
        }

        [RelayCommand]
        private void RunRecentSearch(SearchParameters recent)
        {
            // shortcut: "Last 7 days" is stored as its absolute dates, so re-running it later repeats that week;
            // store the "Show" choice instead if that turns out to confuse people
            SearchParams = recent.Clone();
            LoadForm();
            RunSearch();
        }

        private void LoadRecentSearches()
        {
            _recentSearches = _runtimeDataService.RuntimeData.RecentSearches
                .Select(s => new RecentSearchItem(Describe(s), s))
                .ToList();
        }

        // Subscribed only while this page is shown: the navigation service may build a form it then discards.
        public override void OnBeforeShown()
        {
            base.OnBeforeShown();
            _searchService.PropertyChanged += SearchService_PropertyChanged;
            Results.OnBeforeShown();
            RefreshPane();
        }

        public override void OnBeforeHidden()
        {
            _searchService.PropertyChanged -= SearchService_PropertyChanged;
            base.OnBeforeHidden();
        }

        private void SearchService_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(ISearchService.IsSearching) or nameof(ISearchService.HasSearched)
                or nameof(ISearchService.Videos))
            {
                if (e.PropertyName == nameof(ISearchService.IsSearching) && !_searchService.IsSearching)
                {
                    // the finished search was added to the recent list
                    LoadRecentSearches();
                    FirePropertyChanged(nameof(RecentSearches));
                    FirePropertyChanged(nameof(HasRecentSearches));
                }

                RefreshPane();
            }
        }

        private void RefreshPane()
        {
            FirePropertyChanged(nameof(IsSearching));
            FirePropertyChanged(nameof(ShowResults));
            FirePropertyChanged(nameof(ShowNoResults));
            FirePropertyChanged(nameof(ShowEmptyState));
        }

        private void RunSearch()
        {
            try
            {
                lock (_commandLockObject)
                {
                    _dialogService.SetBusy();
                    // pasted names can carry whitespace/newlines that fail the channel lookup (#56)
                    SearchParams.Channel = SearchParams.Channel?.Trim();
                    Validate();

                    if (!HasErrors)
                    {
                        _searchService.PerformSearch(SearchParams);
                    }
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowAndLogException(ex);
            }
        }

        private void Cancel()
        {
            try
            {
                lock (_commandLockObject)
                {
                    _navigationService.NavigateBack();
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowAndLogException(ex);
            }
        }

        public override void Validate(string propertyName = null)
        {
            base.Validate(propertyName);

            string currentProperty = nameof(SearchParams);

            if (string.IsNullOrWhiteSpace(propertyName) || propertyName == currentProperty)
            {
                SearchParams.Validate();

                if (
                    SearchParams.SearchType == SearchType.Channel
                    && !string.IsNullOrWhiteSpace(SearchParams.Channel)
                    && !_apiService.ChannelExists(SearchParams.Channel)
                )
                {
                    SearchParams.AddError(
                        nameof(SearchParams.Channel),
                        "The specified channel does not exist on Twitch!"
                    );
                }

                // The links box is bound to LinksText, so show the Urls/Ids errors there
                string linksError = (
                    SearchParams.GetErrors(nameof(SearchParameters.Urls))
                    ?? SearchParams.GetErrors(nameof(SearchParameters.Ids))
                )?.Cast<string>().FirstOrDefault();

                if (linksError != null)
                {
                    AddError(nameof(LinksText), linksError);
                }

                if (SearchParams.HasErrors)
                {
                    AddError(currentProperty, "Invalid Search Parameters!");
                }
            }
        }


        protected override List<MenuCommand> BuildMenu()
        {
            List<MenuCommand> menuCommands = base.BuildMenu();

            if (menuCommands == null)
            {
                menuCommands = new List<MenuCommand>();
            }

            // the sidebar has its own Search button; the page needs no bottom-bar buttons

            return menuCommands;
        }

        #endregion Methods
    }
}