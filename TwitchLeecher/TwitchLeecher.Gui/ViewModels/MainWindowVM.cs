using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using TwitchLeecher.Core;
using TwitchLeecher.Core.Enums;
using TwitchLeecher.Core.Events;
using TwitchLeecher.Core.Models;
using TwitchLeecher.Gui.Events;
using TwitchLeecher.Gui.Interfaces;
using TwitchLeecher.Gui.Types;
using TwitchLeecher.Services.Interfaces;
using TwitchLeecher.Shared.Commands;
using TwitchLeecher.Shared.Events;
using TwitchLeecher.Shared.Extensions;
using TwitchLeecher.Shared.Notification;
using TwitchLeecher.Shared.Reflection;

namespace TwitchLeecher.Gui.ViewModels
{
    public class MainWindowVM : BindableBase
    {
        #region Fields

        private bool _showDonationButton;

        private bool _isAuthenticated;
        private bool _isAuthenticatedSubOnly;

        private bool _showAuthError;
        private bool _showAuthSubOnlyError;

        private int _videosCount;
        private int _downloadsCount;

        private ViewModelBase _mainView;

        private readonly IEventAggregator _eventAggregator;
        private readonly IAuthService _authService;
        private readonly IAutoDownloadService _autoDownloadService;
        private readonly IApiService _apiService;
        private DispatcherTimer _tokenCheckTimer;
        private bool _tokenPromptShown;
        private readonly IScheduleService _scheduleService;
        private readonly IDownloadService _downloadService;
        private readonly IDialogService _dialogService;
        private readonly IDonationService _donationService;
        private readonly INavigationService _navigationService;
        private readonly IRuntimeDataService _runtimeDataService;
        private readonly ISearchService _searchService;
        private readonly IPreferencesService _preferencesService;
        private readonly IUpdateService _updateService;

        private ICommand _showSearchCommand;
        private ICommand _showDownloadsCommand;
        private ICommand _showSubOnlyCommand;
        private ICommand _showPreferencesCommand;
        private ICommand _donateCommand;
        private ICommand _showInfoCommand;
        private ICommand _revokeCommand;

        private readonly object _commandLockObject;
        private bool _showNotification;
        private string _notification;
        private int _notificationVersion;

        #endregion Fields

        #region Constructors

        public MainWindowVM(
            IEventAggregator eventAggregator,
            IAuthService authService,
            IAutoDownloadService autoDownloadService,
            IScheduleService scheduleService,
            IDownloadService downloadService,
            IDialogService dialogService,
            IDonationService donationService,
            INavigationService navigationService,
            IRuntimeDataService runtimeDataService,
            ISearchService searchService,
            IPreferencesService preferencesService,
            IUpdateService updateService,
            IApiService apiService)
        {
            AssemblyUtil au = AssemblyUtil.Get;

            Title = au.GetProductName() + " " + au.GetAssemblyVersion().Trim();

            _eventAggregator = eventAggregator;
            _authService = authService;
            _autoDownloadService = autoDownloadService;
            _apiService = apiService;
            _scheduleService = scheduleService;
            _downloadService = downloadService;
            _dialogService = dialogService;
            _donationService = donationService;
            _navigationService = navigationService;
            _runtimeDataService = runtimeDataService;
            _searchService = searchService;
            _preferencesService = preferencesService;
            _updateService = updateService;

            _commandLockObject = new object();

            _eventAggregator.GetEvent<AuthChangedEvent>().Subscribe(AuthChanged);
            _eventAggregator.GetEvent<AuthResultEvent>().Subscribe(AuthResultAction);
            _eventAggregator.GetEvent<SubOnlyAuthChangedEvent>().Subscribe(SubOnlyAuthChanged);
            _eventAggregator.GetEvent<PreferencesSavedEvent>().Subscribe(PreferencesSaved);
            _eventAggregator.GetEvent<VideosCountChangedEvent>().Subscribe(VideosCountChanged);
            _eventAggregator.GetEvent<DownloadsCountChangedEvent>().Subscribe(DownloadsCountChanged);
            _eventAggregator.GetEvent<ShowViewEvent>().Subscribe(ShowView);

            _showDonationButton = _preferencesService.CurrentPreferences.AppShowDonationButton;
        }

        #endregion Constructors

        #region Properties

        public int VideosCount
        {
            get { return _videosCount; }
            private set { SetProperty(ref _videosCount, value, nameof(VideosCount)); }
        }

        // the count pill on the Downloads tab is hidden while the queue is empty
        public bool ShowDownloadsCount => _downloadsCount > 0;

        public int DownloadsCount
        {
            get { return _downloadsCount; }
            private set
            {
                SetProperty(ref _downloadsCount, value, nameof(DownloadsCount));
                FirePropertyChanged(nameof(ShowDownloadsCount));
            }
        }

        public string Title { get; }

        public bool ShowDonationButton
        {
            get { return _showDonationButton; }

            private set { SetProperty(ref _showDonationButton, value, nameof(ShowDonationButton)); }
        }

        public bool IsAuthenticated
        {
            get { return _isAuthenticated; }

            private set
            {
                SetProperty(ref _isAuthenticated, value, nameof(IsAuthenticated));
                FirePropertyChanged(nameof(UserName));

                if (value)
                {
                    LoadUserAvatar();
                }
            }
        }

        private Bitmap _userAvatar;

        // the signed-in user's Twitch profile picture, shown next to "Signed in as" in Settings
        public Bitmap UserAvatar
        {
            get => _userAvatar;
            private set => SetProperty(ref _userAvatar, value);
        }

        private async void LoadUserAvatar()
        {
            string login = UserName;

            if (string.IsNullOrWhiteSpace(login))
            {
                // the sign-in name arrives with the token check, which can finish just after sign-in
                await Task.Delay(2500);
                login = UserName;
            }

            if (string.IsNullOrWhiteSpace(login) || _userAvatar != null)
            {
                return;
            }

            try
            {
                var info = await Task.Run(() => _apiService.GetChannelInfo(login));

                if (info != null && Uri.TryCreate(info.Value.ProfileImageUrl, UriKind.Absolute, out Uri url))
                {
                    UserAvatar = await ImageHelper.LoadFromWeb(url);
                }
            }
            catch (Exception ex)
            {
                // cosmetic: the row works without the picture
                Console.WriteLine("Could not load the profile picture: " + ex.Message);
            }
        }

        // shown in Settings next to Log out
        public string UserName => _authService.UserName;

        public bool IsAuthenticatedSubOnly
        {
            get { return _isAuthenticatedSubOnly; }

            private set
            {
                SetProperty(ref _isAuthenticatedSubOnly, value, nameof(IsAuthenticatedSubOnly));
                FirePropertyChanged(nameof(SubOnlyLabel));
            }
        }

        public string SubOnlyLabel => IsAuthenticatedSubOnly ? "Sub-only: on" : "Sub-only: off";

        public ViewModelBase MainView
        {
            get { return _mainView; }
            set
            {
                SetProperty(ref _mainView, value, nameof(MainView));
                FirePropertyChanged(nameof(IsSearchActive));
                FirePropertyChanged(nameof(IsDownloadsActive));
            }
        }

        public bool IsSearchActive => _mainView is SearchViewModel or SearchResultViewModel;

        public bool IsDownloadsActive => _mainView is DownloadsViewModel;

        public ICommand ShowSearchCommand => _showSearchCommand ??= new DelegateCommand(ShowSearch);

        public ICommand RevokeCommand => _revokeCommand ??= new DelegateCommand(RevokeAuthentication);

        public ICommand ShowSubOnlyCommand => _showSubOnlyCommand ??= new DelegateCommand(ShowSubOnly);

        public ICommand ShowDownloadsCommand => _showDownloadsCommand ??= new DelegateCommand(ShowDownloads);

        public ICommand ShowPreferencesCommand => _showPreferencesCommand ??= new DelegateCommand(ShowPreferences);

        public ICommand DonateCommand => _donateCommand ??= new DelegateCommand(Donate);

        public ICommand ShowInfoCommand => _showInfoCommand ??= new DelegateCommand(ShowInfo);

        public bool ShowNotification
        {
            get => _showNotification;
            set => SetProperty(ref _showNotification, value);
        }

        public string Notification
        {
            get => _notification;
            set => SetProperty(ref _notification, value);
        }

        #endregion Properties

        #region Methods

        private void ShowSearch()
        {
            try
            {
                lock (_commandLockObject)
                {
                    // Always the form; it links back to the results when there are any
                    _navigationService.ShowSearch();
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowAndLogException(ex);
            }
        }

        private void ShowDownloads()
        {
            try
            {
                lock (_commandLockObject)
                {
                    _navigationService.ShowDownloads();
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowAndLogException(ex);
            }
        }

        private void ShowPreferences()
        {
            try
            {
                lock (_commandLockObject)
                {
                    _navigationService.ShowPreferences();
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowAndLogException(ex);
            }
        }

        private void Donate()
        {
            try
            {
                lock (_commandLockObject)
                {
                    _donationService.OpenDonationPage();
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowAndLogException(ex);
            }
        }

        private void ShowInfo()
        {
            try
            {
                lock (_commandLockObject)
                {
                    _navigationService.ShowInfo();
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowAndLogException(ex);
            }
        }


        private void ShowSubOnly()
        {
            lock (_commandLockObject)
            {
                _navigationService.ShowAuthSubOnly();
            }
        }


        private void RevokeAuthentication()
        {
            LogOut(null, null);
        }

        // question/caption: null = the normal "Do you really want to logout?"
        private async void LogOut(string question, string caption)
        {
            try
            {
                _downloadService.Pause();

                MessageBoxResult? result = null;

                if (_downloadService.CanShutdown())
                {
                    result = await _dialogService.ShowMessageBox(question ?? "Do you really want to logout?", caption ?? "Logout",
                        MessageBoxButton.YesNo, MessageBoxImage.Question);
                }
                else
                {
                    result = await _dialogService.ShowMessageBox(
                        "Do you want to abort all running downloads and logout?",
                        "Logout", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                }

                if (result == MessageBoxResult.Yes)
                {
                    _downloadService.Shutdown();
                    _authService.RevokeAuthentication();
                    _navigationService.ShowAuth();
                }
                else
                {
                    _downloadService.Resume();
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowAndLogException(ex);
            }
        }


        private void ShowView(ViewModelBase contentVM)
        {
            if (contentVM != null)
            {
                MainView = contentVM;
            }
        }

        private void PreferencesSaved()
        {
            try
            {
                ShowDonationButton = _preferencesService.CurrentPreferences.AppShowDonationButton;
            }
            catch (Exception ex)
            {
                _dialogService.ShowAndLogException(ex);
            }
        }

        private void VideosCountChanged(int count)
        {
            VideosCount = count;
        }

        private void DownloadsCountChanged(int count)
        {
            DownloadsCount = count;
        }

        public void Loaded()
        {
            try
            {
                CheckAuthenticationSubOnly();

                if (!CheckAuthentication())
                {
                    _navigationService.ShowAuth();
                }
                else
                {
                    ShowSearchOnStart();
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowAndLogException(ex);
            }
        }

        // Twitch asks apps to re-check saved tokens about once an hour; a token stops working when the user
        // changes their password, signs out everywhere, or removes the app's access on twitch.tv
        private void StartTokenChecks()
        {
            if (_tokenCheckTimer != null)
            {
                return;
            }

            _tokenCheckTimer = new DispatcherTimer(TimeSpan.FromHours(1), DispatcherPriority.Background,
                async (_, _) => await CheckTokens());
            _tokenCheckTimer.Start();
        }

        private async Task CheckTokens()
        {
            AuthInfo authInfo = _runtimeDataService.RuntimeData.AuthInfo;

            if (_tokenPromptShown || authInfo == null || string.IsNullOrWhiteSpace(authInfo.AccessToken))
            {
                return;
            }

            (bool? main, bool? subOnly) = await Task.Run(() => (
                _apiService.CheckToken(authInfo.AccessToken, false),
                string.IsNullOrWhiteSpace(authInfo.AccessTokenSubOnly)
                    ? (bool?)true
                    : _apiService.CheckToken(authInfo.AccessTokenSubOnly, true)));

            if (main == false)
            {
                PromptSignInAgain("Your Twitch sign-in has stopped working. Log out now and sign in again?");
            }
            else if (subOnly == false)
            {
                PromptSignInAgain("Sub-only downloads have stopped working. Log out now and sign in again to turn them back on?");
            }
        }

        // asked once per run, so a "No" is not repeated every hour
        private void PromptSignInAgain(string question)
        {
            _tokenPromptShown = true;
            LogOut(question, "Twitch sign-in");
        }

        public void Shown()
        {
            ShowUpdateDialog();

            if (_showAuthError)
            {
                _dialogService.ShowMessageBox(
                    "Twitch could not validate the saved access token. Please try to log in again!", "Login Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }

            if (_showAuthSubOnlyError)
            {
                PromptSignInAgain("Sub-only downloads have stopped working. Log out now and sign in again to turn them back on?");
            }

            StartTokenChecks();
        }

        private bool CheckAuthentication()
        {
            AuthInfo authInfo = _runtimeDataService.RuntimeData.AuthInfo;

            if (authInfo != null && !string.IsNullOrWhiteSpace(authInfo.AccessToken))
            {
                bool valid = _authService.ValidateAuthentication(authInfo.AccessToken, false);

                _showAuthError = !valid;

                return valid;
            }

            return false;
        }

        private void CheckAuthenticationSubOnly()
        {
            AuthInfo authInfo = _runtimeDataService.RuntimeData.AuthInfo;

            if (authInfo != null && !string.IsNullOrWhiteSpace(authInfo.AccessTokenSubOnly))
            {
                _showAuthSubOnlyError = !_authService.ValidateAuthentication(authInfo.AccessTokenSubOnly, true);
            }
        }

        private void AuthChanged(bool isAuthenticated)
        {
            this.IsAuthenticated = isAuthenticated;
        }

        private void AuthResultAction(bool success)
        {
            if (success)
            {
                ShowSearchOnStart();
            }
            else
            {
                _navigationService.ShowAuth();
            }
        }

        private void SubOnlyAuthChanged(bool isAuthenticatedSubOnly)
        {
            this.IsAuthenticatedSubOnly = isAuthenticatedSubOnly;
        }

        private void ShowSearchOnStart()
        {
            // signed in now, so the Twitch API works; the loop itself waits for the setting to be on
            _autoDownloadService.Start();
            _scheduleService.Start();

            _navigationService.ShowSearch();
            SearchOnStartup();
        }

        private bool SearchOnStartup()
        {
            Preferences currentPrefs = _preferencesService.CurrentPreferences.Clone();

            if (currentPrefs.SearchOnStartup)
            {
                currentPrefs.Validate();

                if (!currentPrefs.HasErrors)
                {
                    SearchParameters searchParams = new SearchParameters(currentPrefs.SearchDefaultFavourites ? SearchType.Favorites : SearchType.Channel)
                    {
                        Channel = currentPrefs.SearchChannelName,
                        VideoType = currentPrefs.SearchVideoType,
                        LoadLimitType = currentPrefs.SearchLoadLimitType,
                        LoadFrom = DateTime.Now.Date.AddDays(-currentPrefs.SearchLoadLastDays),
                        LoadFromDefault = DateTime.Now.Date.AddDays(-currentPrefs.SearchLoadLastDays),
                        LoadTo = DateTime.Now.Date,
                        LoadToDefault = DateTime.Now.Date,
                        LoadLastVods = currentPrefs.SearchLoadLastVods
                    };

                    _searchService.PerformSearch(searchParams);

                    return true;
                }
            }

            return false;
        }

        private void ShowUpdateDialog()
        {
            Preferences currentPrefs = _preferencesService.CurrentPreferences.Clone();

            if (currentPrefs.AppCheckForUpdates)
            {
                Task.Run(() => _updateService.CheckForUpdate()).ContinueWith(t =>
                {
                    if (!t.IsFaulted && t.Result != null)
                    {
                        _dialogService.ShowUpdateInfoDialog(t.Result);
                    }
                }, TaskScheduler.FromCurrentSynchronizationContext());
            }
        }

        #endregion Methods

        public void SetNotification(string text)
        {
            Notification = text;
            ShowNotification = true;
            int version = ++_notificationVersion;
            Dispatcher.UIThread.InvokeAsync(async () =>
            {
                await Task.Delay(5000);
                // a newer toast restarts the 5 s timer instead of being hidden early by this one
                if (version == _notificationVersion)
                {
                    ShowNotification = false;
                }
            });
        }
    }
}