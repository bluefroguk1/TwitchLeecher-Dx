using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Input;
using Avalonia;
using CommunityToolkit.Mvvm.Input;
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
        private readonly IQuickDownloadService _quickDownloadService;
        private readonly INotificationService _notificationService;

        private readonly object _commandLockObject;

        private ICommand _viewCommand;
        private ICommand _downloadCommand;
        private ICommand _seachCommand;

        #endregion Fields

        #region Constructors

        public SearchResultViewModel(
            IEventAggregator eventAggregator,
            IDialogService dialogService,
            INavigationService navigationService,
            IPreferencesService preferencesService,
            ISearchService searchService,
            IQuickDownloadService quickDownloadService,
            INotificationService notificationService)
        {
            _eventAggregator = eventAggregator;
            _dialogService = dialogService;
            _navigationService = navigationService;
            _preferencesService = preferencesService;
            _searchService = searchService;
            _quickDownloadService = quickDownloadService;
            _notificationService = notificationService;

            _searchService.PropertyChanged += SearchService_PropertyChanged;

            _commandLockObject = new object();

        }

        #endregion Constructors

        #region Properties

        public Vector ScrollPosition { get; set; }

        public ObservableCollection<TwitchVideo> Videos
        {
            get { return _searchService.Videos; }
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
            }
        }

        #endregion EventHandlers
    }
}