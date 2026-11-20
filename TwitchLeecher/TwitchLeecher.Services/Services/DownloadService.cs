using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using TwitchLeecher.Core.Enums;
using TwitchLeecher.Core.Events;
using TwitchLeecher.Core.Models;
using TwitchLeecher.Services.Interfaces;
using TwitchLeecher.Shared.Events;
using TwitchLeecher.Shared.Extensions;
using TwitchLeecher.Shared.IO;
using TwitchLeecher.Shared.Notification;
using TwitchLeecher.Shared.Reflection;

namespace TwitchLeecher.Services.Services
{
    internal class DownloadService : BindableBase, IDownloadService, IDisposable
    {
        #region Constants

        // milliseconds; was 2ms, which kept a core busy polling the queue even when idle
        private const int TIMER_INTERVALL = 1000;
        private const int DOWNLOAD_RETRIES = 3;
        private const int DOWNLOAD_RETRY_TIME = 5;

        // one shared client so segment downloads reuse connections instead of a new TLS handshake per chunk
        private static readonly HttpClient _httpClient = new HttpClient();

        #endregion Constants

        #region Fields

        private bool disposedValue = false;

        private readonly IApiService _apiService;
        private readonly IPreferencesService _preferencesService;
        private readonly IProcessingService _processingService;
        private readonly IEventAggregator _eventAggregator;

        private readonly Timer _downloadTimer;

        private ObservableCollection<TwitchVideoDownload> _downloads;

        private ConcurrentDictionary<string, DownloadTask> _downloadTasks;

        private readonly object _changeDownloadLockObject;

        private volatile bool _paused;

        #endregion Fields

        #region Constructors

        public DownloadService(
            IApiService apiService,
            IPreferencesService preferencesService,
            IProcessingService processingService,
            IEventAggregator eventAggregator)
        {
            _apiService = apiService;
            _preferencesService = preferencesService;
            _processingService = processingService;
            _eventAggregator = eventAggregator;

            _downloads = new ObservableCollection<TwitchVideoDownload>();
            _downloads.CollectionChanged += Downloads_CollectionChanged;

            _downloadTasks = new ConcurrentDictionary<string, DownloadTask>();

            _changeDownloadLockObject = new object();

            _downloadTimer = new Timer(DownloadTimerCallback, null, 0, TIMER_INTERVALL);

            _eventAggregator.GetEvent<RemoveDownloadEvent>().Subscribe(Remove, ThreadOption.UIThread);
        }

        #endregion Constructors

        #region Properties

        public ObservableCollection<TwitchVideoDownload> Downloads
        {
            get { return _downloads; }
            private set
            {
                if (_downloads != null)
                {
                    _downloads.CollectionChanged -= Downloads_CollectionChanged;
                }

                SetProperty(ref _downloads, value, nameof(Downloads));

                if (_downloads != null)
                {
                    _downloads.CollectionChanged += Downloads_CollectionChanged;
                }

                FireDownloadsCountChanged();
            }
        }

        #endregion Properties

        #region Methods

        public void Enqueue(DownloadParameters downloadParams)
        {
            Enqueue(downloadParams, false);
        }

        public void Enqueue(DownloadParameters downloadParams, bool startPaused)
        {
            // Queue even while the service is paused; the timer picks it up on resume.
            lock (_changeDownloadLockObject)
            {
                TwitchVideoDownload download = new TwitchVideoDownload(downloadParams);

                if (startPaused)
                {
                    download.SetDownloadState(DownloadState.Paused);
                }

                _downloads.Add(download);
            }
        }

        private void DownloadTimerCallback(object state)
        {
            if (_paused)
            {
                return;
            }

            StartQueuedDownloadIfExists();
        }

        private void StartQueuedDownloadIfExists()
        {
            if (_paused)
            {
                return;
            }

            if (Monitor.TryEnter(_changeDownloadLockObject))
            {
                try
                {
                    if (!_downloads.Where(d => d.DownloadState == DownloadState.Downloading).Any())
                    {
                        TwitchVideoDownload download = _downloads.Where(d => d.DownloadState == DownloadState.Queued)
                            .FirstOrDefault();

                        if (download == null)
                        {
                            return;
                        }

                        DownloadParameters downloadParams = download.DownloadParams;

                        CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
                        CancellationToken cancellationToken = cancellationTokenSource.Token;

                        string downloadId = download.Id;
                        string vodId = downloadParams.Video.Id;
                        string tempDir = GetTempDir(downloadId);
                        string ffmpegFile = _processingService.FFMPEGExe;
                        string concatFile = Path.Combine(tempDir,
                            Path.GetFileNameWithoutExtension(downloadParams.FullPath) + ".ts");
                        string outputFile = downloadParams.FullPath;

                        bool disableConversion = downloadParams.DisableConversion;
                        bool cropStart = downloadParams.CropStart;
                        bool cropEnd = downloadParams.CropEnd;

                        TimeSpan cropStartTime = downloadParams.CropStartTime;
                        TimeSpan cropEndTime = downloadParams.CropEndTime;

                        TwitchVideoQuality quality = downloadParams.SelectedQuality;

                        Action<DownloadState> setDownloadState = download.SetDownloadState;
                        Action<string> log = download.AppendLog;
                        Action<string> setStatus = download.SetStatus;
                        Action<double> setProgress = download.SetProgress;
                        Action<bool> setIsIndeterminate = download.SetIsIndeterminate;

                        // Names the step that is running, so a failure can say what went wrong.
                        string stage = "Couldn't start the download";

                        download.PauseRequested = false;
                        download.ResetTransfer();

                        Task<IEnumerable<string>> downloadVideoTask = new Task<IEnumerable<string>>(() =>
                        {
                            setStatus("Initializing");

                            // A paused download keeps its temp folder with the chunks already fetched.
                            bool resuming = Directory.Exists(tempDir);

                            log(resuming ? "Download task has been resumed!" : "Download task has been started!");

                            WriteDownloadInfo(log, downloadParams, ffmpegFile, tempDir);

                            cancellationToken.ThrowIfCancellationRequested();

                            if (downloadParams.Video.IsClip)
                            {
                                DownloadClip(log, setStatus, setIsIndeterminate, downloadParams, outputFile);

                                List<string> clipWarnings = new List<string>();

                                if (downloadParams.SaveVodInfo)
                                {
                                    SaveVodInfo(log, downloadParams, outputFile, clipWarnings);
                                }

                                return clipWarnings;
                            }

                            stage = "Couldn't get VOD access";
                            log(Environment.NewLine + Environment.NewLine + "Retrieving VOD access information...");
                            TwitchVideoAuthInfo vodAuthInfo = _apiService.GetVodAuthInfo(vodId);
                            log(" done!");

                            cancellationToken.ThrowIfCancellationRequested();

                            WriteVodAuthInfo(log, vodAuthInfo);

                            cancellationToken.ThrowIfCancellationRequested();

                            stage = "Couldn't create the temp folder";
                            CheckTempDirectory(log, tempDir, resuming);

                            cancellationToken.ThrowIfCancellationRequested();

                            stage = "Couldn't load the playlist";
                            log(Environment.NewLine + Environment.NewLine +
                                "Retrieving playlist information for all VOD qualities...");
                            Dictionary<TwitchVideoQuality, string> playlistInfo =
                                _apiService.GetPlaylistInfo(vodId, vodAuthInfo);
                            log(" done!");

                            cancellationToken.ThrowIfCancellationRequested();

                            WritePlaylistInfo(log, playlistInfo);

                            cancellationToken.ThrowIfCancellationRequested();

                            TwitchPlaylist vodPlaylist = GetVodPlaylist(log, tempDir, playlistInfo, quality);

                            cancellationToken.ThrowIfCancellationRequested();

                            CropInfo cropInfo = CropVodPlaylist(vodPlaylist, cropStart, cropEnd, cropStartTime,
                                cropEndTime);

                            cancellationToken.ThrowIfCancellationRequested();

                            stage = "Network error";
                            var downloadWarnings = DownloadParts(log, setStatus, setProgress, download, vodPlaylist,
                                cancellationToken);

                            cancellationToken.ThrowIfCancellationRequested();

                            stage = "Couldn't create the output folder";
                            CheckOutputDirectory(log, Path.GetDirectoryName(outputFile));

                            cancellationToken.ThrowIfCancellationRequested();

                            // A pause after an earlier join leaves a half-written join file behind.
                            FileSystem.DeleteFile(concatFile);

                            IEnumerable<string> processingWarnings;

                            // shortcut: crop-start needs input-side -ss, which cannot seek on a pipe, so it keeps the
                            // merged temp file; revisit if ffmpeg gains seekable stdin or crop-start moves to output -ss
                            if (!disableConversion && !cropInfo.CropStart)
                            {
                                stage = "Conversion failed";
                                try
                                {
                                    processingWarnings = _processingService.ConvertParts(log, setStatus, setProgress,
                                        setIsIndeterminate, vodPlaylist, outputFile, cropInfo, cancellationToken);
                                }
                                catch (Exception ex) when (ex is not OperationCanceledException)
                                {
                                    // the temp folder is wiped afterwards; keep the unconverted video instead of losing the download (#49)
                                    string fallbackFile = Path.ChangeExtension(outputFile, ".ts");
                                    _processingService.ConcatParts(log, setStatus, setProgress, vodPlaylist, fallbackFile);
                                    log(Environment.NewLine + Environment.NewLine + "Conversion failed, kept unconverted video as '" + fallbackFile + "'");
                                    stage = "Conversion failed; kept .ts";
                                    throw;
                                }
                            }
                            else
                            {
                                stage = "Joining chunks failed";
                                processingWarnings = _processingService.ConcatParts(log, setStatus, setProgress,
                                    vodPlaylist, disableConversion ? outputFile : concatFile);

                                if (!disableConversion)
                                {
                                    cancellationToken.ThrowIfCancellationRequested();
                                    stage = "Conversion failed";
                                    try
                                    {
                                        _processingService.ConvertVideo(log, setStatus, setProgress, setIsIndeterminate,
                                            concatFile, outputFile, cropInfo);
                                    }
                                    catch (Exception) when (File.Exists(concatFile))
                                    {
                                        // the temp folder is wiped afterwards; keep the merged video instead of losing the download (#49)
                                        string fallbackFile = Path.ChangeExtension(outputFile, ".ts");
                                        File.Move(concatFile, fallbackFile, true);
                                        log(Environment.NewLine + Environment.NewLine + "Conversion failed, kept unconverted video as '" + fallbackFile + "'");
                                        stage = "Conversion failed; kept .ts";
                                        throw;
                                    }
                                }
                            }

                            List<string> warnings = new List<string>();
                            warnings.AddRange(downloadWarnings);
                            warnings.AddRange(processingWarnings);

                            if (downloadParams.DownloadChat)
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                                SaveChat(log, setStatus, setProgress, setIsIndeterminate, downloadParams, outputFile,
                                    warnings, cancellationToken);
                            }

                            if (downloadParams.SaveVodInfo)
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                                SaveVodInfo(log, downloadParams, outputFile, warnings);
                            }

                            return warnings;
                        }, cancellationToken);

                        Task continueTask = downloadVideoTask.ContinueWith((Task<IEnumerable<string>> task) =>
                        {
                            download.SetTransferring(false);
                            setIsIndeterminate(false);

                            bool pausing = task.IsCanceled && download.PauseRequested;
                            download.PauseRequested = false;

                            string failureReason = null;

                            if (task.IsFaulted)
                            {
                                failureReason = GetFailureReason(stage, task.Exception);

                                if (stage == "Conversion failed" && KeepConcatFile(concatFile, outputFile, log))
                                {
                                    failureReason += "; kept .ts";
                                }
                            }

                            if (!pausing)
                            {
                                log(Environment.NewLine + Environment.NewLine +
                                    "Starting temporary download folder cleanup!");
                                CleanUp(tempDir, log);

                                setProgress(100);
                            }

                            bool success = false;

                            if (pausing)
                            {
                                setDownloadState(DownloadState.Paused);
                                log(Environment.NewLine + Environment.NewLine +
                                    "Download task was paused! Finished chunks are kept in '" + tempDir + "'.");
                            }
                            else if (task.IsFaulted)
                            {
                                download.SetFailureReason(failureReason);
                                setDownloadState(DownloadState.Error);
                                log(Environment.NewLine + Environment.NewLine + "Download task ended with an error! (" +
                                    failureReason + ")");

                                if (task.Exception != null)
                                {
                                    log(Environment.NewLine + Environment.NewLine + task.Exception.ToString());
                                }
                            }
                            else if (task.IsCanceled)
                            {
                                setDownloadState(DownloadState.Canceled);
                                log(Environment.NewLine + Environment.NewLine + "Download task was canceled!");
                            }
                            else
                            {
                                success = true;
                                var warnings = task.Result.ToArray();
                                if (warnings.Any())
                                {
                                    var warningsString = string.Join("\n", warnings.Distinct());
                                    setDownloadState(DownloadState.CompletedWithWarning);
                                    log(Environment.NewLine + Environment.NewLine +
                                        $"Download completed with warnings:\n{warningsString}");
                                }
                                else
                                {
                                    setDownloadState(DownloadState.Done);
                                    log(Environment.NewLine + Environment.NewLine +
                                        "Download task ended successfully!");
                                }
                            }

                            if (!_downloadTasks.TryRemove(downloadId, out DownloadTask downloadTask))
                            {
                                throw new ApplicationException("Could not remove download task with ID '" + downloadId +
                                                               "' from download task collection!");
                            }

                            if (success && _preferencesService.CurrentPreferences.DownloadRemoveCompleted)
                            {
                                _eventAggregator.GetEvent<RemoveDownloadEvent>().Publish(downloadId);
                            }
                        });

                        if (_downloadTasks.TryAdd(downloadId,
                                new DownloadTask(downloadVideoTask, continueTask, cancellationTokenSource)))
                        {
                            downloadVideoTask.Start();
                            setDownloadState(DownloadState.Downloading);
                        }
                    }
                }
                finally
                {
                    Monitor.Exit(_changeDownloadLockObject);
                }
            }
        }

        private void WriteDownloadInfo(Action<string> log, DownloadParameters downloadParams, string ffmpegFile,
            string tempDir)
        {
            log(Environment.NewLine + Environment.NewLine + "TWITCH LEECHER INFO");
            log(Environment.NewLine +
                "--------------------------------------------------------------------------------------------");
            log(Environment.NewLine + "Version: " + AssemblyUtil.Get.GetAssemblyVersion().Trim());

            log(Environment.NewLine + Environment.NewLine + "VOD INFO");
            log(Environment.NewLine +
                "--------------------------------------------------------------------------------------------");
            log(Environment.NewLine + "VOD ID: " + downloadParams.Video.Id);
            log(Environment.NewLine + "Selected Quality: " + downloadParams.SelectedQuality.DisplayString);
            log(Environment.NewLine + "Download Url: " + downloadParams.Video.Url);
            log(Environment.NewLine + "Crop Start: " + (downloadParams.CropStart
                ? "Yes (" + downloadParams.CropStartTime.ToDaylessString() + ")"
                : "No"));
            log(Environment.NewLine + "Crop End: " + (downloadParams.CropEnd
                ? "Yes (" + downloadParams.CropEndTime.ToDaylessString() + ")"
                : "No"));

            log(Environment.NewLine + Environment.NewLine + "OUTPUT INFO");
            log(Environment.NewLine +
                "--------------------------------------------------------------------------------------------");
            log(Environment.NewLine + "Disable Conversion: " + (downloadParams.DisableConversion ? "Yes" : "No"));
            log(Environment.NewLine + "Output File: " + downloadParams.FullPath);
            log(Environment.NewLine + "FFMPEG Path: " + ffmpegFile);
            log(Environment.NewLine + "Temporary Download Folder: " + tempDir);
        }

        private void WriteVodAuthInfo(Action<string> log, TwitchVideoAuthInfo vodAuthInfo)
        {
            log(Environment.NewLine + Environment.NewLine + "ACCESS INFO");
            log(Environment.NewLine +
                "--------------------------------------------------------------------------------------------");
            log(Environment.NewLine + "Token: " + vodAuthInfo.Token);
            log(Environment.NewLine + "Signature: " + vodAuthInfo.Signature);
            log(Environment.NewLine + "Sub-Only: " + (vodAuthInfo.SubOnly ? "Yes" : "No"));
            log(Environment.NewLine + "Privileged: " + (vodAuthInfo.Privileged ? "Yes" : "No"));
        }

        private void WritePlaylistInfo(Action<string> log, Dictionary<TwitchVideoQuality, string> playlistInfo)
        {
            log(Environment.NewLine + Environment.NewLine + "PLAYLIST INFO");
            log(Environment.NewLine +
                "--------------------------------------------------------------------------------------------");

            foreach (var entry in playlistInfo)
            {
                log(Environment.NewLine + "Playlist for quality '" + entry.Key.Name + "' is '" + entry.Value + "'");
            }
        }

        private string GetTempDir(string downloadId)
        {
            return Path.Combine(_preferencesService.CurrentPreferences.DownloadTempFolder, downloadId);
        }

        private static string GetFailureReason(string stage, AggregateException exception)
        {
            Exception inner = exception?.Flatten().InnerException;

            if (inner is IOException && !(inner is FileNotFoundException))
            {
                return "Disk error: " + inner.Message;
            }

            if ((inner is WebException || inner is System.Net.Http.HttpRequestException) && stage != "Network error")
            {
                return stage + " (network error)";
            }

            return stage;
        }

        /// <summary>Moves the joined but unconverted .ts next to the wanted output, so a failed conversion loses nothing.</summary>
        private static bool KeepConcatFile(string concatFile, string outputFile, Action<string> log)
        {
            try
            {
                string target = Path.ChangeExtension(outputFile, ".ts");

                if (!File.Exists(concatFile) || File.Exists(target))
                {
                    return false;
                }

                File.Move(concatFile, target);
                log(Environment.NewLine + Environment.NewLine + "Kept the unconverted video as '" + target + "'");
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void CheckTempDirectory(Action<string> log, string tempDir, bool resuming)
        {
            if (!Directory.Exists(tempDir))
            {
                log(Environment.NewLine + Environment.NewLine + "Creating temporary download directory '" + tempDir +
                    "'...");
                FileSystem.CreateDirectory(tempDir);
                log(" done!");
            }

            if (!resuming && Directory.EnumerateFileSystemEntries(tempDir).Any())
            {
                throw new ApplicationException("Temporary download directory '" + tempDir + "' is not empty!");
            }
        }

        private void CheckOutputDirectory(Action<string> log, string outputDir)
        {
            if (!Directory.Exists(outputDir))
            {
                log(Environment.NewLine + Environment.NewLine + "Creating output directory '" + outputDir + "'...");
                FileSystem.CreateDirectory(outputDir);
                log(" done!");
            }
        }

        private TwitchPlaylist GetVodPlaylist(Action<string> log, string tempDir,
            Dictionary<TwitchVideoQuality, string> playlistInfo, TwitchVideoQuality selectedQuality)
        {
            TwitchVideoQuality quality = playlistInfo.Keys.First(q => q.Equals(selectedQuality));

            string playlistUrl = playlistInfo[quality];

            log(Environment.NewLine + Environment.NewLine + "Playlist url for selected quality '" +
                quality.DisplayString + "' is '" + playlistUrl + "'");

            using (WebClient webClient = new WebClient())
            {
                log(Environment.NewLine + Environment.NewLine + "Retrieving playlist...");
                string playlistStr = webClient.DownloadString(playlistUrl);
                log(" done!");

                if (string.IsNullOrWhiteSpace(playlistStr))
                {
                    throw new ApplicationException("The playlist is empty!");
                }

                string urlPrefix = playlistUrl.Substring(0, playlistUrl.LastIndexOf("/") + 1);

                log(Environment.NewLine + "Parsing playlist...");
                TwitchPlaylist vodPlaylist = TwitchPlaylist.Parse(tempDir, playlistStr, urlPrefix);
                log(" done!");

                log(Environment.NewLine + "Number of video chunks: " + vodPlaylist.Count());

                return vodPlaylist;
            }
        }

        private CropInfo CropVodPlaylist(TwitchPlaylist vodPlaylist, bool cropStart, bool cropEnd,
            TimeSpan cropStartTime, TimeSpan cropEndTime)
        {
            double start = cropStartTime.TotalMilliseconds;
            double end = cropEndTime.TotalMilliseconds;
            double length = cropEndTime.TotalMilliseconds;

            if (cropStart)
            {
                length -= start;
            }

            start = Math.Round(start / 1000, 3);
            end = Math.Round(end / 1000, 3);
            length = Math.Round(length / 1000, 3);

            List<TwitchPlaylistPart> deleteStart = new List<TwitchPlaylistPart>();
            List<TwitchPlaylistPart> deleteEnd = new List<TwitchPlaylistPart>();

            if (cropStart)
            {
                double lengthSum = 0;

                foreach (TwitchPlaylistPart part in vodPlaylist)
                {
                    double partLength = part.Length;

                    if (lengthSum + partLength < start)
                    {
                        lengthSum += partLength;
                        deleteStart.Add(part);
                    }
                    else
                    {
                        start = Math.Round(start - lengthSum, 3);
                        break;
                    }
                }
            }

            if (cropEnd)
            {
                double lengthSum = 0;

                foreach (TwitchPlaylistPart part in vodPlaylist)
                {
                    if (lengthSum >= end)
                    {
                        deleteEnd.Add(part);
                    }

                    lengthSum += part.Length;
                }
            }

            deleteStart.ForEach(part => { vodPlaylist.Remove(part); });

            deleteEnd.ForEach(part => { vodPlaylist.Remove(part); });

            return new CropInfo(cropStart, cropEnd, cropStart ? start : 0, length);
        }

        private IEnumerable<string> DownloadParts(Action<string> log, Action<string> setStatus,
            Action<double> setProgress,
            TwitchVideoDownload download,
            TwitchPlaylist vodPlaylist,
            CancellationToken cancellationToken)
        {
            var warnings = new ConcurrentBag<string>();
            int partsCount = vodPlaylist.Count;
            int maxConnectionCount = _preferencesService.CurrentPreferences.MaxConnectionCount;

            log(Environment.NewLine + Environment.NewLine + "Starting parallel video chunk download");
            log(Environment.NewLine + "Number of video chunks to download: " + partsCount);
            log(Environment.NewLine + "Maximum connection count: " + maxConnectionCount);

            setStatus("Downloading");

            if (vodPlaylist.InitSegment != null && !File.Exists(vodPlaylist.InitSegment.LocalFile))
            {
                log(Environment.NewLine + Environment.NewLine + "Downloading VOD init segment...");
                DownloadInitSegment(vodPlaylist.InitSegment, log);
                log(" done!");
            }

            log(Environment.NewLine + Environment.NewLine + "Parallel video chunk download is running...");

            long completedPartDownloads = 0;

            // Chunks kept from before a pause count as done, but not towards the speed.
            foreach (TwitchPlaylistPart part in vodPlaylist.Where(p => File.Exists(p.LocalFile)))
            {
                completedPartDownloads++;
                download.AddDownloadedBytes(new FileInfo(part.LocalFile).Length,
                    (double)completedPartDownloads / partsCount);
            }

            if (completedPartDownloads > 0)
            {
                log(Environment.NewLine + "Chunks kept from before the pause: " + completedPartDownloads);
                setProgress((double)completedPartDownloads / partsCount * 100);
            }

            download.SetTransferring(true);

            Parallel.ForEach(vodPlaylist.Where(p => !File.Exists(p.LocalFile)).ToList(),
                new ParallelOptions() { MaxDegreeOfParallelism = Math.Max(1, maxConnectionCount - 1) },
                (part, loopState) =>
                {
                    int retryCounter = 0;

                    bool success = false;

                    do
                    {
                        try
                        {
                            byte[] bytes = _httpClient.GetByteArrayAsync(part.RemoteFile).GetAwaiter().GetResult();

                            // Write then rename, so a chunk file on disk is always complete (resume trusts it).
                            string partFile = part.LocalFile + ".part";
                            File.WriteAllBytes(partFile, bytes);
                            File.Move(partFile, part.LocalFile, true);

                            long completed = Interlocked.Increment(ref completedPartDownloads);

                            download.AddDownloadedBytes(bytes.Length, (double)completed / partsCount);
                            setProgress((double)completed / partsCount * 100);

                            success = true;
                        }
                        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
                        {
                            if (ex is HttpRequestException { StatusCode: not null })
                            {
                                warnings.Add("Video contains missing frames");
                                log($"{Environment.NewLine}File '{part.RemoteFile}' is not available, skipping");

                                long completed = Interlocked.Increment(ref completedPartDownloads);

                                setProgress((double)completed / partsCount * 100);

                                success = true;
                                continue;
                            }

                            if (retryCounter < DOWNLOAD_RETRIES)
                            {
                                retryCounter++;
                                log(Environment.NewLine + Environment.NewLine + "Downloading file '" + part.RemoteFile +
                                    "' failed! Trying again in " + DOWNLOAD_RETRY_TIME + "s");
                                log(Environment.NewLine + ex);
                                Thread.Sleep(DOWNLOAD_RETRY_TIME * 1000);
                            }
                            else
                            {
                                // Out of retries: fail the download instead of spinning on this chunk forever.
                                throw;
                            }
                        }
                    } while (!success);

                    if (cancellationToken.IsCancellationRequested)
                    {
                        loopState.Stop();
                    }
                });

            download.SetTransferring(false);

            cancellationToken.ThrowIfCancellationRequested();

            setProgress(100);

            log(Environment.NewLine + Environment.NewLine + "Download of all video chunks complete!");
            return warnings;
        }

        private void DownloadInitSegment(TwitchPlaylistPart initSegment, Action<string> log)
        {
            int retryCounter = 0;

            while (true)
            {
                try
                {
                    byte[] bytes = _httpClient.GetByteArrayAsync(initSegment.RemoteFile).GetAwaiter().GetResult();

                    string partFile = initSegment.LocalFile + ".part";
                    File.WriteAllBytes(partFile, bytes);
                    File.Move(partFile, initSegment.LocalFile, true);

                    return;
                }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
                {
                    if (retryCounter >= DOWNLOAD_RETRIES)
                    {
                        throw;
                    }

                    retryCounter++;
                    log(Environment.NewLine + Environment.NewLine + "Downloading init segment '" + initSegment.RemoteFile +
                        "' failed! Trying again in " + DOWNLOAD_RETRY_TIME + "s");
                    log(Environment.NewLine + ex);
                    Thread.Sleep(DOWNLOAD_RETRY_TIME * 1000);
                }
            }
        }

        // Clips are one small mp4: no playlist, chunks or ffmpeg step (#37)
        private void DownloadClip(Action<string> log, Action<string> setStatus, Action<bool> setIsIndeterminate,
            DownloadParameters downloadParams, string outputFile)
        {
            setStatus("Downloading");
            setIsIndeterminate(true);

            log(Environment.NewLine + Environment.NewLine + "Retrieving clip download URL...");
            // signed clip URLs expire, so get a fresh one when the download actually starts
            Dictionary<TwitchVideoQuality, string> qualities = _apiService.GetClipQualities(downloadParams.Video.Id);
            log(" done!");

            if (!qualities.TryGetValue(downloadParams.SelectedQuality, out string clipUrl))
            {
                throw new ApplicationException("Quality '" + downloadParams.SelectedQuality.DisplayString +
                                               "' is no longer available for this clip!");
            }

            CheckOutputDirectory(log, Path.GetDirectoryName(outputFile));

            string partFile = outputFile + ".part";

            log(Environment.NewLine + Environment.NewLine + "Downloading clip to '" + outputFile + "'...");

            try
            {
                using (WebClient webClient = new WebClient())
                {
                    webClient.DownloadFile(clipUrl, partFile);
                }

                File.Move(partFile, outputFile, true);
            }
            catch
            {
                FileSystem.DeleteFile(partFile);
                throw;
            }

            log(" done!");
        }

        private void SaveChat(Action<string> log, Action<string> setStatus, Action<double> setProgress,
            Action<bool> setIsIndeterminate, DownloadParameters downloadParams, string outputFile,
            List<string> warnings, CancellationToken cancellationToken)
        {
            // same window as the video, so chat timestamps line up with the downloaded file
            TimeSpan from = downloadParams.CropStart ? downloadParams.CropStartTime : TimeSpan.Zero;
            TimeSpan to = downloadParams.CropEnd ? downloadParams.CropEndTime : downloadParams.Video.Length;
            bool html = downloadParams.ChatFormat != ChatFormat.Text;
            string chatFile = Path.ChangeExtension(outputFile, html ? ".chat.html" : ".chat.txt");

            setStatus("Saving Chat");
            setIsIndeterminate(false);
            setProgress(0);
            log(Environment.NewLine + Environment.NewLine + "Downloading chat to '" + chatFile + "'...");

            try
            {
                List<TwitchChatMessage> messages = _apiService.GetChatMessages(downloadParams.Video.Id, from, to,
                    offset =>
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        if (to > from)
                        {
                            setProgress((offset - from).TotalSeconds / (to - from).TotalSeconds * 100);
                        }
                    });

                if (html)
                {
                    Dictionary<string, string> emoteSources = downloadParams.ChatFormat == ChatFormat.HtmlEmbedded
                        ? DownloadEmotes(messages, log, cancellationToken)
                        : null;

                    File.WriteAllText(chatFile,
                        ChatHtmlWriter.Write(downloadParams.Video.Title, messages, from, emoteSources));
                }
                else
                {
                    File.WriteAllLines(chatFile, messages.Select(m =>
                        "[" + (m.Offset - from).ToDaylessString() + "] " + m.Name + ": " + m.Text));
                }

                log(" done! (" + messages.Count + " messages)");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // the video is already saved; losing chat should not fail the download
                warnings.Add("Chat could not be saved");
                log(Environment.NewLine + "Saving chat failed:" + Environment.NewLine + ex);
            }

            setProgress(100);
        }

        private void SaveVodInfo(Action<string> log, DownloadParameters downloadParams, string outputFile,
            List<string> warnings)
        {
            string infoFile = Path.ChangeExtension(outputFile, ".info.json");

            log(Environment.NewLine + Environment.NewLine + "Saving VOD info to '" + infoFile + "'...");

            try
            {
                TwitchVideo video = downloadParams.Video;

                var info = new
                {
                    title = video.Title,
                    channel = video.Channel,
                    id = video.Id,
                    url = video.Url?.ToString(),
                    recordedDate = video.RecordedDate.ToString("o"),
                    length = video.Length.ToString(@"hh\:mm\:ss"),
                    lengthSeconds = (int)video.Length.TotalSeconds,
                    views = video.Views,
                    quality = downloadParams.SelectedQuality.DisplayString,
                    cropStart = downloadParams.CropStart ? downloadParams.CropStartTime.ToString(@"hh\:mm\:ss") : null,
                    cropEnd = downloadParams.CropEnd ? downloadParams.CropEndTime.ToString(@"hh\:mm\:ss") : null,
                    downloadedLength = downloadParams.CroppedLength.ToString(@"hh\:mm\:ss"),
                    videoFile = Path.GetFileName(outputFile)
                };

                File.WriteAllText(infoFile, System.Text.Json.JsonSerializer.Serialize(info,
                    new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));

                log(" done!");
            }
            catch (Exception ex)
            {
                // the video is already saved; losing the info file should not fail the download
                warnings.Add("VOD info could not be saved");
                log(Environment.NewLine + "Saving VOD info failed:" + Environment.NewLine + ex);
            }
        }

        // Downloads each distinct emote once as a data: URI so the HTML works offline; failed emotes are left out
        private static Dictionary<string, string> DownloadEmotes(List<TwitchChatMessage> messages, Action<string> log,
            CancellationToken cancellationToken)
        {
            Dictionary<string, string> sources = new Dictionary<string, string>();
            HashSet<string> ids = new HashSet<string>(messages.SelectMany(m => m.Fragments)
                .Where(f => f.EmoteId != null).Select(f => f.EmoteId));

            log(Environment.NewLine + "Embedding " + ids.Count + " emotes...");

            using (WebClient webClient = new WebClient())
            {
                foreach (string id in ids)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        string base64 = Convert.ToBase64String(webClient.DownloadData(ChatHtmlWriter.EmoteUrl(id)));
                        // animated emotes come back as GIF, so keep the server's type
                        string type = webClient.ResponseHeaders?[HttpResponseHeader.ContentType] ?? "image/png";
                        sources[id] = "data:" + type + ";base64," + base64;
                    }
                    catch (WebException ex)
                    {
                        log(Environment.NewLine + "Emote " + id + " could not be downloaded: " + ex.Message);
                    }
                }
            }

            return sources;
        }

        private void CleanUp(string directory, Action<string> log)
        {
            try
            {
                log(Environment.NewLine + "Deleting directory '" + directory + "'...");
                FileSystem.DeleteDirectory(directory);
                log(" done!");
            }
            catch
            {
            }
        }

        public void Cancel(string id)
        {
            lock (_changeDownloadLockObject)
            {
                if (_downloadTasks.TryGetValue(id, out DownloadTask downloadTask))
                {
                    downloadTask.CancellationTokenSource.Cancel();
                }
            }
        }

        public void Retry(string id)
        {
            if (_paused)
            {
                return;
            }

            lock (_changeDownloadLockObject)
            {
                if (!_downloadTasks.TryGetValue(id, out DownloadTask downloadTask))
                {
                    TwitchVideoDownload download = _downloads.Where(d => d.Id == id).FirstOrDefault();

                    if (download != null && (download.DownloadState == DownloadState.Canceled ||
                                             download.DownloadState == DownloadState.Error))
                    {
                        download.ResetLog();
                        download.ResetTransfer();
                        download.SetProgress(0);
                        download.SetDownloadState(DownloadState.Queued);
                        download.SetStatus("Initializing");
                    }
                }
            }
        }

        public void Remove(string id)
        {
            lock (_changeDownloadLockObject)
            {
                if (!_downloadTasks.TryGetValue(id, out DownloadTask downloadTask))
                {
                    TwitchVideoDownload download = _downloads.Where(d => d.Id == id).FirstOrDefault();

                    if (download != null)
                    {
                        if (download.DownloadState == DownloadState.Paused)
                        {
                            CleanUp(GetTempDir(id), _ => { });
                        }

                        _downloads.Remove(download);
                    }
                }
            }
        }

        public void Pause(string id)
        {
            lock (_changeDownloadLockObject)
            {
                TwitchVideoDownload download = _downloads.FirstOrDefault(d => d.Id == id);

                if (download == null)
                {
                    return;
                }

                if (download.DownloadState == DownloadState.Queued)
                {
                    download.SetDownloadState(DownloadState.Paused);
                }
                else if (download.DownloadState == DownloadState.Downloading &&
                         _downloadTasks.TryGetValue(id, out DownloadTask downloadTask))
                {
                    // The continuation sees PauseRequested and parks the item as Paused, keeping its chunks.
                    download.PauseRequested = true;
                    download.SetStatus("Pausing");
                    downloadTask.CancellationTokenSource.Cancel();
                }
            }
        }

        public void Resume(string id)
        {
            lock (_changeDownloadLockObject)
            {
                TwitchVideoDownload download = _downloads.FirstOrDefault(d => d.Id == id);

                // A task still in the collection is the paused one finishing up; resume after it is gone.
                if (download != null && download.DownloadState == DownloadState.Paused &&
                    !_downloadTasks.ContainsKey(id))
                {
                    download.SetDownloadState(DownloadState.Queued);
                }
            }
        }

        public void MoveUp(string id)
        {
            lock (_changeDownloadLockObject)
            {
                int index = _downloads.IndexOf(_downloads.FirstOrDefault(d => d.Id == id));

                // Swap with the nearest waiting item above; finished or running items don't affect queue order.
                for (int target = index - 1; target >= 0; target--)
                {
                    DownloadState state = _downloads[target].DownloadState;

                    if (state == DownloadState.Queued || state == DownloadState.Paused)
                    {
                        _downloads.Move(index, target);
                        return;
                    }
                }
            }
        }

        public void ClearFinished()
        {
            lock (_changeDownloadLockObject)
            {
                foreach (TwitchVideoDownload download in _downloads
                             .Where(d => d.IsFinished && !_downloadTasks.ContainsKey(d.Id)).ToList())
                {
                    _downloads.Remove(download);
                }
            }
        }

        public void Pause()
        {
            _paused = true;
            _downloadTimer.Change(Timeout.Infinite, Timeout.Infinite);
        }

        public void Resume()
        {
            _paused = false;
            _downloadTimer.Change(0, TIMER_INTERVALL);
        }

        public bool CanShutdown()
        {
            Monitor.Enter(_changeDownloadLockObject);

            try
            {
                return !_downloads.Where(d =>
                    d.DownloadState == DownloadState.Downloading || d.DownloadState == DownloadState.Queued ||
                    d.DownloadState == DownloadState.Paused).Any();
            }
            finally
            {
                Monitor.Exit(_changeDownloadLockObject);
            }
        }

        public void Shutdown()
        {
            Pause();

            foreach (DownloadTask downloadTask in _downloadTasks.Values)
            {
                downloadTask.CancellationTokenSource.Cancel();
            }

            List<Task> tasks = _downloadTasks.Values.Select(v => v.Task).ToList();
            tasks.AddRange(_downloadTasks.Values.Select(v => v.ContinueTask).ToList());

            try
            {
                Task.WaitAll(tasks.ToArray());
            }
            catch (Exception)
            {
                // Don't care about aborted tasks
            }

            List<string> toRemove = _downloads.Select(d => d.Id).ToList();

            foreach (string id in toRemove)
            {
                Remove(id);
            }
        }

        public bool IsFileNameUsed(string fullPath)
        {
            IEnumerable<TwitchVideoDownload> downloads = _downloads.Where(d =>
                d.DownloadState == DownloadState.Downloading || d.DownloadState == DownloadState.Queued ||
                d.DownloadState == DownloadState.Paused);

            foreach (TwitchVideoDownload download in downloads)
            {
                if (download.DownloadParams.FullPath.Equals(fullPath, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private void FireDownloadsCountChanged()
        {
            _eventAggregator.GetEvent<DownloadsCountChangedEvent>().Publish(_downloads != null ? _downloads.Count : 0);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _downloadTimer.Dispose();
                }

                _downloads = null;
                _downloadTasks = null;

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }

        #endregion Methods

        #region EventHandlers

        private void Downloads_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            FireDownloadsCountChanged();
        }

        #endregion EventHandlers
    }
}