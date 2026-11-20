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
        private const int SHOW_CUSTOM = 3;

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
            INotificationService notificationService
        )
        {
            _apiService = apiService;
            _searchService = searchService;
            _dialogService = dialogService;
            _navigationService = navigationService;
            _preferencesService = preferencesService;
            _eventAggregator = eventAggregator;
            _eventAggregator.GetEvent<LogEvent>().Subscribe(LogEvent);
            _notificationService = notificationService;

            _commandLockObject = new object();

            AssemblyUtil au = AssemblyUtil.Get;
            ProductTitle = au.GetProductName() + " " + au.GetAssemblyVersion().Trim();

            RecentSearches = runtimeDataService.RuntimeData.RecentSearches
                .Select(s => new RecentSearchItem(Describe(s), s))
                .ToList();

            _searchParams = _searchService.LastSearchParams.Clone();
            LoadForm();
        }

        #endregion Constructors

        #region Properties

        public void LogEvent(string message)
        {
            _notificationService.ShowNotification(message);
        }

        public SearchParameters SearchParams
        {
            get { return _searchParams; }
            set { SetProperty(ref _searchParams, value, nameof(SearchParams)); }
        }

        public string ProductTitle { get; }

        public List<RecentSearchItem> RecentSearches { get; }

        public bool HasRecentSearches => RecentSearches.Count > 0;

        public RangeObservableCollection<string> FavChannels
        {
            get { return _preferencesService.CurrentPreferences.SearchFavouriteChannels; }
        }

        public bool HasFavChannels => FavChannels.Count > 0;

        // The VM is created on each visit to Search, so the count is read once (no change tracking needed)
        public bool HasResults => _searchService.Videos?.Count > 0;

        public string BackToResultsText => "\u2190 Back to results (" + (_searchService.Videos?.Count ?? 0) + ")";

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

                    default:
                        SearchParams.LoadLimitType = LoadLimitType.Timespan;
                        break;
                }

                FirePropertyChanged(nameof(IsLastVods));
                FirePropertyChanged(nameof(IsCustomRange));
            }
        }

        public bool IsLastVods => _showIndex == SHOW_LAST_VODS;

        public bool IsCustomRange => _showIndex == SHOW_CUSTOM;

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
                _ => SHOW_CUSTOM
            };

            FirePropertyChanged(nameof(ShowIndex));
            FirePropertyChanged(nameof(IsLastVods));
            FirePropertyChanged(nameof(IsCustomRange));
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
        private void BackToResults()
        {
            _navigationService.ShowSearchResults();
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

        private void RunSearch()
        {
            try
            {
                lock (_commandLockObject)
                {
                    _dialogService.SetBusy();
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

        [RelayCommand]
        private void OpenSearchDropDown()
        {
            SearchParams.Channel = string.Empty;
            IsDropdownOpen = !IsDropdownOpen;
        }

        protected override List<MenuCommand> BuildMenu()
        {
            List<MenuCommand> menuCommands = base.BuildMenu();

            if (menuCommands == null)
            {
                menuCommands = new List<MenuCommand>();
            }

            menuCommands.Add(
                new MenuCommand(SearchCommand, "Search", "fa-solid fa-magnifying-glass")
            );
            menuCommands.Add(new MenuCommand(CancelCommand, "Cancel", "fa-solid fa-xmark"));

            return menuCommands;
        }

        #endregion Methods
    }
}