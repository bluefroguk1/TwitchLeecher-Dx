using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Avalonia.Threading;
using TwitchLeecher.Core;
using TwitchLeecher.Core.Enums;
using TwitchLeecher.Core.Events;
using TwitchLeecher.Core.Models;
using TwitchLeecher.Gui.Interfaces;
using TwitchLeecher.Services.Interfaces;
using TwitchLeecher.Shared.Events;

namespace TwitchLeecher.Gui.ViewModels.Results
{
    // Keeps the QUEUED and DOWNLOADED badges of the results up to date: watches the download queue and
    // the preferences, caches each video's default save path.
    internal class ResultStatusTracker
    {
        private readonly IPreferencesService _preferencesService;
        private readonly IDownloadService _downloadService;
        private readonly IQuickDownloadService _quickDownloadService;
        private readonly IDialogService _dialogService;
        private readonly Func<ObservableCollection<TwitchVideo>> _videos;

        // default save path per video id, so badges don't rebuild filenames on every queue change
        private readonly Dictionary<string, string> _defaultPaths = new Dictionary<string, string>();
        private bool _statusRefreshPending;

        // raised on the UI thread after a queue/preferences-triggered refresh; the argument is true when
        // any DOWNLOADED badge changed
        public event Action<bool> Refreshed;

        public ResultStatusTracker(
            IEventAggregator eventAggregator,
            IPreferencesService preferencesService,
            IDownloadService downloadService,
            IQuickDownloadService quickDownloadService,
            IDialogService dialogService,
            Func<ObservableCollection<TwitchVideo>> videos)
        {
            _preferencesService = preferencesService;
            _downloadService = downloadService;
            _quickDownloadService = quickDownloadService;
            _dialogService = dialogService;
            _videos = videos;

            // queue changes may come from a background thread (e.g. automatic downloads); a finished download
            // changes state without leaving the list, so watch each item's state too
            _downloadService.Downloads.CollectionChanged += Downloads_CollectionChanged;

            foreach (TwitchVideoDownload download in _downloadService.Downloads)
            {
                download.PropertyChanged += Download_PropertyChanged;
            }

            // folder or filename settings may have changed
            eventAggregator.GetEvent<PreferencesSavedEvent>().Subscribe(() =>
            {
                _defaultPaths.Clear();
                RequestRefresh();
            });
        }

        public void ClearPathCache() => _defaultPaths.Clear();

        // many queue events in a row (Download Selected, a watched-search check) cause one refresh, not one each
        public void RequestRefresh()
        {
            if (_statusRefreshPending)
            {
                return;
            }

            _statusRefreshPending = true;
            Dispatcher.UIThread.Post(() =>
            {
                _statusRefreshPending = false;
                Refreshed?.Invoke(Refresh());
            }, DispatcherPriority.Background);
        }

        // Sets the QUEUED and DOWNLOADED badges; true when any DOWNLOADED badge changed
        public bool Refresh()
        {
            bool downloadedChanged = false;

            try
            {
                Preferences prefs = _preferencesService.CurrentPreferences;

                List<TwitchVideoDownload> active = _downloadService.Downloads
                    .Where(d => d.DownloadState == DownloadState.Queued || d.DownloadState == DownloadState.Paused ||
                                d.DownloadState == DownloadState.Downloading)
                    .ToList();
                HashSet<string> queuedIds = active.Select(d => d.DownloadParams.Video.Id).ToHashSet();
                HashSet<string> queuedPaths = active.Select(d => d.DownloadParams.FullPath)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                // shortcut: only finds files saved under the default folder/filename, and not at all when the
                // filename uses {res} (the quality is unknown until the playlist is fetched); upgrade by keeping
                // a record of finished downloads by video id if renamed files should count too
                bool canGuessPath = !string.IsNullOrWhiteSpace(prefs.DownloadFileName) &&
                                    !prefs.DownloadFileName.Contains(FilenameWildcards.RES);
                TwitchVideoQuality anyQuality = new TwitchVideoQuality("chunked", "Source");

                // one directory listing per folder instead of a File.Exists per result
                Dictionary<string, HashSet<string>> folderFiles = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

                foreach (TwitchVideo video in _videos() ?? new ObservableCollection<TwitchVideo>())
                {
                    string path = null;

                    if (canGuessPath && !_defaultPaths.TryGetValue(video.Id, out path))
                    {
                        path = _quickDownloadService.GetDefaultPath(video, anyQuality);
                        _defaultPaths[video.Id] = path;
                    }

                    bool downloaded = path != null && FileExists(folderFiles, path);
                    downloadedChanged |= downloaded != video.IsDownloaded;
                    video.IsDownloaded = downloaded;
                    video.IsQueued = !video.IsDownloaded &&
                                     (queuedIds.Contains(video.Id) || (path != null && queuedPaths.Contains(path)));
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowAndLogException(ex);
            }

            return downloadedChanged;
        }

        private static bool FileExists(Dictionary<string, HashSet<string>> folderFiles, string path)
        {
            string folder = Path.GetDirectoryName(path) ?? string.Empty;

            if (!folderFiles.TryGetValue(folder, out HashSet<string> files))
            {
                files = Directory.Exists(folder)
                    ? new HashSet<string>(Directory.EnumerateFiles(folder), StringComparer.OrdinalIgnoreCase)
                    : new HashSet<string>();
                folderFiles[folder] = files;
            }

            return files.Contains(path);
        }

        private void Downloads_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            foreach (TwitchVideoDownload download in e.NewItems?.OfType<TwitchVideoDownload>() ?? Enumerable.Empty<TwitchVideoDownload>())
            {
                download.PropertyChanged += Download_PropertyChanged;
            }

            foreach (TwitchVideoDownload download in e.OldItems?.OfType<TwitchVideoDownload>() ?? Enumerable.Empty<TwitchVideoDownload>())
            {
                download.PropertyChanged -= Download_PropertyChanged;
            }

            RequestRefresh();
        }

        private void Download_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(TwitchVideoDownload.DownloadState))
            {
                RequestRefresh();
            }
        }
    }
}
