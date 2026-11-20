using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Input;
using Avalonia;
using Avalonia.Threading;
using TwitchLeecher.Core.Enums;
using TwitchLeecher.Core.Models;
using TwitchLeecher.Gui.Interfaces;
using TwitchLeecher.Gui.Types;
using TwitchLeecher.Services.Interfaces;
using TwitchLeecher.Shared.Commands;

namespace TwitchLeecher.Gui.ViewModels
{
    public class DownloadsViewModel : ViewModelBase, INavigationState
    {
        #region Fields

        private readonly IDialogService _dialogService;
        private readonly IDownloadService _downloadService;
        private readonly INavigationService _navigationService;
        private readonly IPreferencesService _preferencesService;

        private ICommand _retryDownloadCommand;
        private ICommand _cancelDownloadCommand;
        private ICommand _removeDownloadCommand;
        private ICommand _showLogCommand;
        private ICommand _openDownloadFolderCommand;

        private readonly object _commandLockObject;

        private readonly DispatcherTimer _refreshTimer;

        private string _summaryCounts;
        private string _summarySize;
        private double _overallProgress;
        private bool _hasDownloads;
        private bool _canPauseAll;

        #endregion Fields

        #region Constructors

        public DownloadsViewModel(
            IDialogService dialogService,
            IDownloadService downloadService,
            INavigationService navigationService,
            IPreferencesService preferencesService)
        {
            _dialogService = dialogService;
            _downloadService = downloadService;
            _navigationService = navigationService;
            _preferencesService = preferencesService;

            _downloadService.PropertyChanged += DownloadService_PropertyChanged;

            _commandLockObject = new object();

            PauseDownloadCommand = new DelegateCommand<string>(id => RunForId(id, _downloadService.Pause));
            ResumeDownloadCommand = new DelegateCommand<string>(id => RunForId(id, _downloadService.Resume));
            MoveUpCommand = new DelegateCommand<string>(id => RunForId(id, _downloadService.MoveUp));
            RemoveDownloadCommand = new DelegateCommand<string>(id => RunForId(id, _downloadService.Remove));
            PlayCommand = new DelegateCommand<string>(Play);
            PauseAllCommand = new DelegateCommand(PauseOrResumeAll);
            ClearFinishedCommand = new DelegateCommand(ClearFinished);

            // Speed, time left and the summary bar all refresh on this one-second tick.
            _refreshTimer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background,
                (_, _) => Refresh());
            _refreshTimer.Start();

            Refresh();
        }

        #endregion Constructors

        #region Properties

        public Vector ScrollPosition { get; set; }

        public ObservableCollection<TwitchVideoDownload> Downloads
        {
            get { return _downloadService.Downloads; }
        }

        public ICommand PauseDownloadCommand { get; }

        public ICommand ResumeDownloadCommand { get; }

        public ICommand MoveUpCommand { get; }

        public ICommand RemoveDownloadCommand { get; }

        public ICommand PlayCommand { get; }

        public ICommand PauseAllCommand { get; }

        public ICommand ClearFinishedCommand { get; }

        /// <summary>For example "1 downloading · 1 queued · 1 paused · 1 done".</summary>
        public string SummaryCounts
        {
            get { return _summaryCounts; }
            private set { SetProperty(ref _summaryCounts, value); }
        }

        /// <summary>For example "2.1 of 5.3 GB · about 6 min left".</summary>
        public string SummarySize
        {
            get { return _summarySize; }
            private set { SetProperty(ref _summarySize, value); }
        }

        public double OverallProgress
        {
            get { return _overallProgress; }
            private set { SetProperty(ref _overallProgress, value); }
        }

        public bool HasDownloads
        {
            get { return _hasDownloads; }
            private set { SetProperty(ref _hasDownloads, value); }
        }

        /// <summary>True: the button pauses everything waiting or running. False: it resumes everything paused.</summary>
        public bool CanPauseAll
        {
            get { return _canPauseAll; }
            private set
            {
                SetProperty(ref _canPauseAll, value);
                FirePropertyChanged(nameof(PauseAllText));
            }
        }

        public string PauseAllText => CanPauseAll ? "Pause all" : "Resume all";

        public ICommand RetryDownloadCommand
        {
            get
            {
                if (_retryDownloadCommand == null)
                {
                    _retryDownloadCommand = new DelegateCommand<string>(RetryDownload);
                }

                return _retryDownloadCommand;
            }
        }

        public ICommand CancelDownloadCommand
        {
            get
            {
                if (_cancelDownloadCommand == null)
                {
                    _cancelDownloadCommand = new DelegateCommand<string>(CancelDownload);
                }

                return _cancelDownloadCommand;
            }
        }


        public ICommand ShowLogCommand
        {
            get
            {
                if (_showLogCommand == null)
                {
                    _showLogCommand = new DelegateCommand<string>(ShowLog);
                }

                return _showLogCommand;
            }
        }

        public ICommand OpenDownloadFolderCommand
        {
            get
            {
                if (_openDownloadFolderCommand == null)
                {
                    _openDownloadFolderCommand = new DelegateCommand<string>(OpenDownloadFolder);
                }

                return _openDownloadFolderCommand;
            }
        }

        #endregion Properties

        #region Methods

        private void Refresh()
        {
            List<TwitchVideoDownload> downloads = Downloads.ToList();

            foreach (TwitchVideoDownload download in downloads.Where(d => d.IsDownloading))
            {
                download.SampleSpeed();
            }

            List<string> counts = new List<string>();
            AddCount(counts, downloads.Count(d => d.IsDownloading), "downloading");
            AddCount(counts, downloads.Count(d => d.IsQueued), "queued");
            AddCount(counts, downloads.Count(d => d.IsPaused), "paused");
            AddCount(counts, downloads.Count(d => d.IsDone), "done");
            AddCount(counts, downloads.Count(d => d.DownloadState == DownloadState.Error), "failed");
            AddCount(counts, downloads.Count(d => d.DownloadState == DownloadState.Canceled), "canceled");
            SummaryCounts = counts.Count > 0 ? string.Join(" · ", counts) : "No downloads";

            // shortcut: sizes are only known once an item has fetched a chunk, so items still waiting are left
            // out of the totals and time left; upgrade when a size estimate exists before the download starts.
            List<TwitchVideoDownload> sized = downloads
                .Where(d => !d.IsFailed && d.EstimatedTotalBytes > 0).ToList();
            long doneBytes = sized.Sum(d => d.BytesDownloaded);
            long totalBytes = sized.Sum(d => d.EstimatedTotalBytes);
            double speed = downloads.Sum(d => d.BytesPerSecond);

            string size = totalBytes > 0
                ? TwitchVideoDownload.FormatBytes(doneBytes) + " of " + TwitchVideoDownload.FormatBytes(totalBytes)
                : "";

            if (speed > 0 && totalBytes > doneBytes)
            {
                size += " · about " +
                        TwitchVideoDownload.FormatTimeLeft(TimeSpan.FromSeconds((totalBytes - doneBytes) / speed)) +
                        " left";
            }

            SummarySize = size;
            OverallProgress = totalBytes > 0 ? (double)doneBytes / totalBytes * 100 : 0;
            HasDownloads = downloads.Count > 0;
            CanPauseAll = downloads.Any(d => d.IsDownloading || d.IsQueued) || !downloads.Any(d => d.IsPaused);
        }

        private static void AddCount(List<string> counts, int count, string label)
        {
            if (count > 0)
            {
                counts.Add(count + " " + label);
            }
        }

        private void RunForId(string id, Action<string> action)
        {
            try
            {
                lock (_commandLockObject)
                {
                    if (!string.IsNullOrWhiteSpace(id))
                    {
                        action(id);
                    }
                }

                Refresh();
            }
            catch (Exception ex)
            {
                _dialogService.ShowAndLogException(ex);
            }
        }

        private void ClearFinished()
        {
            try
            {
                lock (_commandLockObject)
                {
                    _downloadService.ClearFinished();
                }

                Refresh();
            }
            catch (Exception ex)
            {
                _dialogService.ShowAndLogException(ex);
            }
        }

        private void PauseOrResumeAll()
        {
            bool pause = CanPauseAll;

            foreach (TwitchVideoDownload download in Downloads.ToList())
            {
                if (pause && (download.IsDownloading || download.IsQueued))
                {
                    RunForId(download.Id, _downloadService.Pause);
                }
                else if (!pause && download.IsPaused)
                {
                    RunForId(download.Id, _downloadService.Resume);
                }
            }
        }

        private void Play(string id)
        {
            try
            {
                TwitchVideoDownload download = Downloads.FirstOrDefault(d => d.Id == id);
                string file = download?.DownloadParams.FullPath;

                if (file == null || !File.Exists(file))
                {
                    _dialogService.ShowMessageBox("The downloaded file was not found:" + Environment.NewLine + file,
                        "Play", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                Preferences currentPrefs = _preferencesService.CurrentPreferences;

                if (currentPrefs.MiscUseExternalPlayer && !string.IsNullOrWhiteSpace(currentPrefs.MiscExternalPlayer))
                {
                    Process.Start(currentPrefs.MiscExternalPlayer, "\"" + file + "\"");
                }
                else
                {
                    Process.Start(new ProcessStartInfo(file) { UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowAndLogException(ex);
            }
        }

        private void RetryDownload(string id)
        {
            try
            {
                lock (_commandLockObject)
                {
                    if (!string.IsNullOrWhiteSpace(id))
                    {
                        _downloadService.Retry(id);
                    }
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowAndLogException(ex);
            }
        }

        private void CancelDownload(string id)
        {
            try
            {
                lock (_commandLockObject)
                {
                    if (!string.IsNullOrWhiteSpace(id))
                    {
                        if (Downloads.Single(d => d.Id == id).DownloadState == DownloadState.Downloading)
                        {
                            _downloadService.Cancel(id);
                            return;
                        }

                        _downloadService.Remove(id);
                    }
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowAndLogException(ex);
            }
        }

        private void ShowLog(string id)
        {
            try
            {
                lock (_commandLockObject)
                {
                    if (!string.IsNullOrWhiteSpace(id))
                    {
                        TwitchVideoDownload download = Downloads.Where(d => d.Id == id).FirstOrDefault();

                        if (download != null)
                        {
                            _navigationService.ShowLog(download);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowAndLogException(ex);
            }
        }

        private void OpenDownloadFolder(string id)
        {
            try
            {
                lock (_commandLockObject)
                {
                    if (!string.IsNullOrWhiteSpace(id))
                    {
                        TwitchVideoDownload download = Downloads.FirstOrDefault(d => d.Id == id);

                        if (download != null)
                        {
                            string folder = download.DownloadParams.Folder;

                            if (Directory.Exists(folder))
                            {
                                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                                {
                                    using Process fileOpener = new Process();
                                    fileOpener.StartInfo.FileName = "explorer";
                                    fileOpener.StartInfo.Arguments = folder;
                                    fileOpener.Start();
                                    return;
                                }

                                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                                {
                                    var args =
                                        "--print-reply --dest=org.freedesktop.FileManager1 /org/freedesktop/FileManager1 org.freedesktop.FileManager1.ShowItems array:string:\"file://" +
                                        folder + "/" + download.DownloadParams.Filename + "\" string:\"\"";
                                    using Process dbusShowItemsProcess = new Process
                                    {
                                        StartInfo = new ProcessStartInfo
                                        {
                                            FileName = "dbus-send",
                                            Arguments = args,
                                            UseShellExecute = true
                                        }
                                    };
                                    dbusShowItemsProcess.Start();
                                }
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

        #endregion Methods

        #region EventHandlers

        private void DownloadService_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            FirePropertyChanged(e.PropertyName);
        }

        #endregion EventHandlers
    }
}