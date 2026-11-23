using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TwitchLeecher.Core.Enums;
using TwitchLeecher.Shared.Notification;

namespace TwitchLeecher.Core.Models
{
    public class TwitchVideoDownload : BindableBase
    {
        #region Fields

        private const int LogNotifyIntervalMs = 200;

        private DownloadState _downloadState;
        private readonly object _downloadStateLockObject;

        private readonly StringBuilder _log;
        private readonly object _logLockObject;
        private int _logNotifyPending;

        private double _progress;
        private readonly object _progressLockObject;

        private string _status;
        private readonly object _statusLockObject;

        private bool _isIndeterminate;
        private readonly object _isIndeterminateLockObject;

        private long _bytesDownloaded;
        private double _transferFraction;
        private volatile bool _isTransferring;
        private long _lastSampleBytes;
        private DateTime _lastSampleTime;
        private double _bytesPerSecond;

        private string _failureReason;
        private volatile bool _pauseRequested;
        private volatile bool _cancelRequested;

        #endregion Fields

        #region Constructors

        public TwitchVideoDownload(DownloadParameters downloadParams)
        {
            Id = Guid.NewGuid().ToString("N");
            DownloadParams = downloadParams ?? throw new ArgumentNullException(nameof(downloadParams));

            _log = new StringBuilder();

            _downloadStateLockObject = new object();
            _logLockObject = new object();
            _progressLockObject = new object();
            _statusLockObject = new object();
            _isIndeterminateLockObject = new object();
        }

        #endregion Constructors

        #region Properties

        public string Id { get; }

        public DownloadParameters DownloadParams { get; }

        public DownloadState DownloadState
        {
            get
            {
                return _downloadState;
            }
            private set
            {
                SetProperty(ref _downloadState, value);

                FirePropertyChanged(nameof(IsDone));
                FirePropertyChanged(nameof(CanRetry));
                FirePropertyChanged(nameof(Status));
                FirePropertyChanged(nameof(IsDownloading));
                FirePropertyChanged(nameof(IsQueued));
                FirePropertyChanged(nameof(IsPaused));
                FirePropertyChanged(nameof(IsFailed));
                FirePropertyChanged(nameof(HasWarnings));
                FirePropertyChanged(nameof(StatusChip));
                FirePropertyChanged(nameof(MetaLine));
                FirePropertyChanged(nameof(DetailLine));
            }
        }

        public bool CanRetry
        {
            get
            {
                return DownloadState == DownloadState.Canceled || DownloadState == DownloadState.Error;
            }
        }

        public string Log
        {
            get
            {
                lock (_logLockObject)
                {
                    return _log.ToString();
                }
            }
        }

        public double Progress
        {
            get
            {
                return _progress;
            }
            private set
            {
                SetProperty(ref _progress, value);
            }
        }

        public string Status
        {
            get
            {
                if (_downloadState != DownloadState.Downloading)
                {
                    return _downloadState.GetDescription();
                }

                return _status;
            }
            private set
            {
                SetProperty(ref _status, value);
            }
        }

        public bool IsIndeterminate
        {
            get
            {
                return _isIndeterminate;
            }
            private set
            {
                SetProperty(ref _isIndeterminate, value);
            }
        }

        public bool IsDone => DownloadState == DownloadState.Done || DownloadState == DownloadState.CompletedWithWarning;

        public bool HasWarnings => DownloadState == DownloadState.CompletedWithWarning;

        public bool IsDownloading => DownloadState == DownloadState.Downloading;

        public bool IsQueued => DownloadState == DownloadState.Queued;

        public bool IsPaused => DownloadState == DownloadState.Paused;

        public bool IsFailed => CanRetry;

        public bool IsFinished => IsDone || IsFailed;

        /// <summary>Set by the download service: the active task stops after its in-flight chunks and keeps them.</summary>
        public bool PauseRequested
        {
            get { return _pauseRequested; }
            set { _pauseRequested = value; }
        }

        /// <summary>Set by the download service while a cancel is stopping transfers and cleaning up.</summary>
        public bool CancelRequested
        {
            get { return _cancelRequested; }
            set
            {
                _cancelRequested = value;
                FirePropertyChanged(nameof(DetailLine));
            }
        }

        public string FailureReason
        {
            get { return _failureReason; }
            private set
            {
                SetProperty(ref _failureReason, value);
                FirePropertyChanged(nameof(DetailLine));
            }
        }

        public long BytesDownloaded => Interlocked.Read(ref _bytesDownloaded);

        /// <summary>Bytes of chunks downloaded divided by the share of chunks done; 0 until the first chunk lands.</summary>
        public long EstimatedTotalBytes
        {
            get
            {
                double fraction = Volatile.Read(ref _transferFraction);
                return fraction > 0 ? (long)(BytesDownloaded / fraction) : 0;
            }
        }

        public double BytesPerSecond => _bytesPerSecond;

        public TimeSpan? TimeLeft
        {
            get
            {
                long total = EstimatedTotalBytes;

                if (!_isTransferring || _bytesPerSecond <= 0 || total <= 0)
                {
                    return null;
                }

                return TimeSpan.FromSeconds(Math.Max(0, total - BytesDownloaded) / _bytesPerSecond);
            }
        }

        public string StatusChip
        {
            get
            {
                string chip;

                switch (DownloadState)
                {
                    case DownloadState.CompletedWithWarning: chip = "DONE · WARNINGS"; break;
                    case DownloadState.Error: chip = "FAILED"; break;
                    default: chip = DownloadState.ToString().ToUpperInvariant(); break;
                }

                if (DownloadState == DownloadState.Paused && DownloadParams.WaitForSchedule)
                {
                    chip = "SCHEDULED";
                }

                return DownloadParams.FromWatchedSearch ? chip + " · WATCHED" : chip;
            }
        }

        public string MetaLine
        {
            get
            {
                long total = EstimatedTotalBytes;
                string size = total <= 0
                    ? DownloadParams.CroppedLengthStr
                    : (IsDone ? "" : "~") + FormatBytes(total);

                return DownloadParams.Video.Channel + " · " + size;
            }
        }

        public string DetailLine
        {
            get
            {
                switch (DownloadState)
                {
                    case DownloadState.Downloading:
                        if (_cancelRequested)
                        {
                            return "Cancelling: stopping transfers and cleaning up temporary files";
                        }

                        if (_pauseRequested)
                        {
                            return "Pausing after the chunks in flight";
                        }

                        if (!_isTransferring)
                        {
                            return IsIndeterminate ? Status : Status + " · " + Progress.ToString("0") + "%";
                        }

                        string line = Progress.ToString("0") + "%";

                        if (_bytesPerSecond > 0)
                        {
                            line += " · " + FormatBytes((long)_bytesPerSecond) + "/s";
                        }

                        TimeSpan? left = TimeLeft;

                        if (left.HasValue)
                        {
                            line += " · " + FormatTimeLeft(left.Value) + " left";
                        }

                        return line;

                    case DownloadState.Queued:
                        return string.Empty;

                    case DownloadState.Paused:
                        if (DownloadParams.WaitForSchedule)
                        {
                            return "Waits for the download schedule";
                        }

                        return BytesDownloaded > 0
                            ? "Paused at " + Progress.ToString("0") + "%"
                            : DownloadParams.FromWatchedSearch
                                ? "Found by a watched search, waiting for you"
                                : "Paused, waiting for you";

                    case DownloadState.Done:
                        return "Saved to " + DownloadParams.Folder;

                    case DownloadState.CompletedWithWarning:
                        return "Saved to " + DownloadParams.Folder + " with warnings, see log";

                    case DownloadState.Error:
                        return FailureReason ?? "Failed";

                    default:
                        return "Canceled";
                }
            }
        }

        #endregion Properties

        #region Methods

        public void SetDownloadState(DownloadState downloadState)
        {
            lock (_downloadStateLockObject)
            {
                DownloadState = downloadState;
            }
        }

        public void AppendLog(string text)
        {
            lock (_logLockObject)
            {
                _log.Append(text);
            }

            // Every notification makes the bound log view re-read and re-render the whole
            // text (Log.ToString + TextBox update), so many small appends are coalesced
            // into at most one notification per interval; the last append is always flushed.
            // shortcut: full-text rebind per notification, switch to an incremental line
            // collection if logs reach many MB.
            if (Interlocked.Exchange(ref _logNotifyPending, 1) == 0)
            {
                Task.Delay(LogNotifyIntervalMs).ContinueWith(_ =>
                {
                    Volatile.Write(ref _logNotifyPending, 0);
                    FirePropertyChanged(nameof(Log));
                });
            }
        }

        public void ResetLog()
        {
            lock (_logLockObject)
            {
                _log.Clear();
                FirePropertyChanged(nameof(Log));
            }
        }

        public void SetProgress(double progress)
        {
            lock (_progressLockObject)
            {
                Progress = progress;
            }
        }

        public void SetStatus(string status)
        {
            lock (_statusLockObject)
            {
                Status = status;
            }
        }

        public void SetFailureReason(string reason)
        {
            FailureReason = reason;
        }

        /// <summary>Called from download threads for each finished chunk; fraction is chunks done / chunks total.</summary>
        public void AddDownloadedBytes(long bytes, double fraction)
        {
            Interlocked.Add(ref _bytesDownloaded, bytes);
            Volatile.Write(ref _transferFraction, fraction);
        }

        public void SetTransferring(bool isTransferring)
        {
            _isTransferring = isTransferring;
            _lastSampleBytes = BytesDownloaded;
            _lastSampleTime = DateTime.UtcNow;
            _bytesPerSecond = 0;
        }

        /// <summary>Clears byte counters, e.g. before a retry that starts from scratch.</summary>
        public void ResetTransfer()
        {
            Interlocked.Exchange(ref _bytesDownloaded, 0);
            Volatile.Write(ref _transferFraction, 0);
            SetTransferring(false);
            FailureReason = null;
        }

        /// <summary>Call about once a second: updates the smoothed speed and refreshes the text lines.</summary>
        public void SampleSpeed()
        {
            if (_isTransferring)
            {
                DateTime now = DateTime.UtcNow;
                double seconds = (now - _lastSampleTime).TotalSeconds;

                if (seconds >= 0.5)
                {
                    long bytes = BytesDownloaded;
                    double current = (bytes - _lastSampleBytes) / seconds;

                    // Chunks land in bursts, so smooth hard: 30% new sample, 70% history.
                    _bytesPerSecond = _bytesPerSecond <= 0 ? current : 0.3 * current + 0.7 * _bytesPerSecond;
                    _lastSampleBytes = bytes;
                    _lastSampleTime = now;
                }
            }

            FirePropertyChanged(nameof(MetaLine));
            FirePropertyChanged(nameof(DetailLine));
        }

        public static string FormatBytes(long bytes)
        {
            const double kb = 1024, mb = kb * 1024, gb = mb * 1024;

            if (bytes >= gb) return (bytes / gb).ToString("0.##") + " GB";
            if (bytes >= mb) return (bytes / mb).ToString(bytes >= 10 * mb ? "0" : "0.#") + " MB";
            return Math.Max(1, (long)Math.Round(bytes / kb)) + " KB";
        }

        public static string FormatTimeLeft(TimeSpan left)
        {
            if (left.TotalMinutes < 1) return "under a minute";
            if (left.TotalHours < 1) return Math.Ceiling(left.TotalMinutes) + " min";
            return (int)left.TotalHours + " h " + left.Minutes + " min";
        }

        public void SetIsIndeterminate(bool isIndeterminate)
        {
            lock (_isIndeterminateLockObject)
            {
                IsIndeterminate = isIndeterminate;
            }
        }

        #endregion Methods
    }
}