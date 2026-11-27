using System.ComponentModel;
using System.Globalization;
using System.Windows.Input;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.Input;
using TwitchLeecher.Core;
using TwitchLeecher.Core.Enums;
using TwitchLeecher.Core.Models;
using TwitchLeecher.Gui.Interfaces;
using TwitchLeecher.Gui.Types;
using TwitchLeecher.Services.Interfaces;
using TwitchLeecher.Shared.Commands;
using TwitchLeecher.Shared.Extensions;

namespace TwitchLeecher.Gui.ViewModels
{
    public partial class DownloadViewModel : ViewModelBase
    {
        #region Fields

        private DownloadParameters _downloadParams;
        // the last name made from the template; while the field still shows it, crop/quality changes refresh it
        private string _templateFilename;
        private List<IImage> _previewFrames;
        private bool _previewsLoading;
        private int _previewInterval;

        private ICommand _downloadCommand;
        private ICommand _cancelCommand;

        private readonly IApiService _apiService;
        private readonly IDialogService _dialogService;
        private readonly IDownloadService _downloadService;
        private readonly IFilenameService _filenameService;
        private readonly IPreferencesService _preferencesService;
        private readonly INavigationService _navigationService;
        private readonly INotificationService _notificationService;

        private readonly object _commandLockObject;

        #endregion Fields

        #region Constructors

        public DownloadViewModel(
            IApiService apiService,
            IDialogService dialogService,
            IDownloadService downloadService,
            IFilenameService filenameService,
            IPreferencesService preferencesService,
            INavigationService navigationService,
            INotificationService notificationService)
        {
            _apiService = apiService;
            _dialogService = dialogService;
            _downloadService = downloadService;
            _filenameService = filenameService;
            _preferencesService = preferencesService;
            _navigationService = navigationService;
            _notificationService = notificationService;

            _commandLockObject = new object();
        }

        #endregion Constructors

        #region Properties

        // starts from Settings > Download > Compress; ticking it here only affects this download
        public bool CompressDownload
        {
            get { return _downloadParams?.Compress ?? _preferencesService.CurrentPreferences.DownloadCompress; }
            set
            {
                if (_downloadParams != null)
                {
                    _downloadParams.Compress = value;
                    FirePropertyChanged(nameof(CompressDownload));
                }
            }
        }

        public DownloadParameters DownloadParams
        {
            get { return _downloadParams; }
            set
            {
                if (_downloadParams != null)
                {
                    _downloadParams.PropertyChanged -= DownloadParams_PropertyChanged;
                }

                SetProperty(ref _downloadParams, value, nameof(DownloadParams));

                _downloadParams.PropertyChanged += DownloadParams_PropertyChanged;
                _templateFilename = _downloadParams.Filename;

                if (_downloadParams.CanCrop)
                {
                    // reserve the preview boxes from the first frame, so the dialog doesn't grow when they arrive
                    _previewsLoading = true;
                    _ = LoadPreviewsAsync(_downloadParams);
                }
            }
        }

        public bool ShowMutedPartsWarning
        {
            get { return _downloadParams.Video.Muted; }
        }

        // the dialog lists the formats in ChatFormat order
        public int ChatFormatIndex
        {
            get { return (int)_downloadParams.ChatFormat; }
            set
            {
                // -1 means the ComboBox lost its selection; keep the current format
                if (value < 0)
                {
                    return;
                }

                _downloadParams.ChatFormat = (ChatFormat)value;
                FirePropertyChanged(nameof(ChatFormatIndex));
            }
        }

        // "channel · 16 Nov 2026 · 4:02:55"
        public string VideoDetails
        {
            get
            {
                TwitchVideo video = _downloadParams.Video;
                return video.Channel + " · " + video.RecordedDate.ToString("d MMM yyyy", CultureInfo.CurrentCulture) +
                       " · " + FormatLength(video.Length);
            }
        }

        public double VideoSeconds
        {
            get { return _downloadParams.Video.Length.TotalSeconds; }
        }

        private TimeSpan RangeStartTime
        {
            get { return _downloadParams.CropStart ? _downloadParams.CropStartTime : TimeSpan.Zero; }
        }

        private TimeSpan RangeEndTime
        {
            get { return _downloadParams.CropEnd ? _downloadParams.CropEndTime : _downloadParams.Video.Length; }
        }

        // bound to the filmstrip handles
        public double RangeStart
        {
            get { return RangeStartTime.TotalSeconds; }
            set { SetRange(TimeSpan.FromSeconds(value), RangeEndTime); }
        }

        public double RangeEnd
        {
            get { return RangeEndTime.TotalSeconds; }
            set { SetRange(RangeStartTime, TimeSpan.FromSeconds(value)); }
        }

        // bound to the From/to text fields; a setter exception shows as the field's validation error
        public string RangeStartText
        {
            get { return RangeStartTime.ToDaylessString(); }
            set { SetRange(ParseTime(value), RangeEndTime); }
        }

        public string RangeEndText
        {
            get { return RangeEndTime.ToDaylessString(); }
            set { SetRange(RangeStartTime, ParseTime(value)); }
        }

        // "30:00 · ~1.71 GB"
        public string RangeSummary
        {
            get
            {
                string size = _downloadParams.EstimatedSizeStr;
                string length = FormatLength(_downloadParams.CroppedLength);
                return size == "unknown" ? length : length + " · " + size;
            }
        }

        public bool HasPreviews
        {
            get { return _previewFrames != null; }
        }

        // true until the storyboard has loaded (or failed): placeholders hold the space meanwhile
        public bool PreviewsLoading
        {
            get { return _previewsLoading; }
        }

        public bool ShowPreviewArea
        {
            get { return _previewsLoading || _previewFrames != null; }
        }

        public IReadOnlyList<IImage> PreviewFrames
        {
            get { return _previewFrames; }
        }

        public IImage StartPreview
        {
            get { return FrameAt(RangeStartTime); }
        }

        public IImage EndPreview
        {
            get { return FrameAt(RangeEndTime); }
        }

        public ICommand DownloadCommand
        {
            get
            {
                if (_downloadCommand == null)
                {
                    _downloadCommand = DelegateCommand.FromAsyncHandler(Download);
                }

                return _downloadCommand;
            }
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

        #endregion Properties

        #region Methods

        [RelayCommand]
        private void Choose()
        {
            try
            {
                lock (_commandLockObject)
                {
                    _dialogService.ShowFolderBrowserDialog(_downloadParams.Folder, ChooseCallback);
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowAndLogException(ex);
            }
        }

        private void ChooseCallback(bool cancelled, string folder)
        {
            try
            {
                if (!cancelled)
                {
                    _downloadParams.Folder = folder;
                    _downloadParams.Validate(nameof(DownloadParameters.Folder));
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowAndLogException(ex);
            }
        }

        [RelayCommand]
        private void ResetFilename()
        {
            UpdateFilenameFromTemplate();
        }

        [RelayCommand]
        private void WholeVideo()
        {
            SetRange(TimeSpan.Zero, _downloadParams.Video.Length);
        }

        private void SetRange(TimeSpan start, TimeSpan end)
        {
            TimeSpan length = _downloadParams.Video.Length;

            // values come from whole-second handles/fields; "no crop" at either edge keeps the old full-video behaviour
            _downloadParams.CropStartTime = start;
            _downloadParams.CropStart = start > TimeSpan.Zero;
            _downloadParams.CropEndTime = end;
            _downloadParams.CropEnd = end < length;

            FirePropertyChanged(nameof(RangeStart));
            FirePropertyChanged(nameof(RangeEnd));
            FirePropertyChanged(nameof(RangeStartText));
            FirePropertyChanged(nameof(RangeEndText));
            FirePropertyChanged(nameof(StartPreview));
            FirePropertyChanged(nameof(EndPreview));
        }

        // accepts H:MM:SS, MM:SS or SS
        private TimeSpan ParseTime(string text)
        {
            string[] parts = (text ?? string.Empty).Trim().Split(':');
            long seconds = 0;

            if (parts.Length > 3)
            {
                throw new FormatException("Use HH:MM:SS");
            }

            foreach (string part in parts)
            {
                if (!int.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out int value))
                {
                    throw new FormatException("Use HH:MM:SS");
                }

                seconds = seconds * 60 + value;
            }

            TimeSpan time = TimeSpan.FromSeconds(seconds);

            if (time > _downloadParams.Video.Length)
            {
                throw new FormatException("The video is only " + _downloadParams.Video.Length.ToDaylessString() + " long");
            }

            return time;
        }

        private static string FormatLength(TimeSpan value)
        {
            return value.TotalHours >= 1
                ? value.GetDaysInHours() + ":" + value.ToString(@"mm\:ss", CultureInfo.InvariantCulture)
                : value.ToString(@"mm\:ss", CultureInfo.InvariantCulture);
        }

        private IImage FrameAt(TimeSpan time)
        {
            if (_previewFrames == null)
            {
                return null;
            }

            return _previewFrames[Math.Clamp((int)(time.TotalSeconds / _previewInterval), 0, _previewFrames.Count - 1)];
        }

        // shortcut: previews are Twitch's storyboard stills (one every few seconds to minutes), not exact frames; upgrade by grabbing frames with ffmpeg if exact cut points matter
        private async Task LoadPreviewsAsync(DownloadParameters downloadParams)
        {
            try
            {
                await LoadPreviewsCoreAsync(downloadParams);
            }
            finally
            {
                if (downloadParams == _downloadParams)
                {
                    _previewsLoading = false;
                    FirePropertyChanged(nameof(PreviewsLoading));
                    FirePropertyChanged(nameof(ShowPreviewArea));
                }
            }
        }

        private async Task LoadPreviewsCoreAsync(DownloadParameters downloadParams)
        {
            try
            {
                TwitchVideo video = downloadParams.Video;
                TwitchStoryboard board = await Task.Run(() => _apiService.GetStoryboard(video.Id));

                if (board == null || board.Interval <= 0 || board.Cols <= 0 || board.Rows <= 0)
                {
                    return;
                }

                Bitmap[] sheets = await Task.WhenAll(board.Images.Select(ImageHelper.LoadFromWeb));
                int perSheet = board.Cols * board.Rows;
                // the storyboard pads to a fixed count; frames past the end of the video are blank
                int count = Math.Min(board.Count, (int)Math.Ceiling(video.Length.TotalSeconds / board.Interval));
                List<IImage> frames = new List<IImage>();

                for (int i = 0; i < count; i++)
                {
                    Bitmap sheet = i / perSheet < sheets.Length ? sheets[i / perSheet] : null;
                    int index = i % perSheet;
                    PixelRect rect = new PixelRect(index % board.Cols * board.Width, index / board.Cols * board.Height,
                        board.Width, board.Height);

                    if (sheet == null || rect.Right > sheet.PixelSize.Width || rect.Bottom > sheet.PixelSize.Height)
                    {
                        break;
                    }

                    frames.Add(new CroppedBitmap(sheet, rect));
                }

                if (frames.Count == 0 || downloadParams != _downloadParams)
                {
                    return;
                }

                _previewInterval = board.Interval;
                _previewFrames = frames;

                FirePropertyChanged(nameof(PreviewFrames));
                FirePropertyChanged(nameof(HasPreviews));
                FirePropertyChanged(nameof(StartPreview));
                FirePropertyChanged(nameof(EndPreview));
            }
            catch (Exception ex)
            {
                // previews are optional; the plain range bar stays
                Console.WriteLine("Could not load video previews: " + ex.Message);
            }
        }

        private void UpdateFilenameFromTemplate()
        {
            Preferences currentPrefs = _preferencesService.CurrentPreferences.Clone();

            TimeSpan? cropStartTime = _downloadParams.CropStart ? _downloadParams.CropStartTime : TimeSpan.Zero;
            TimeSpan? cropEndTime =
                _downloadParams.CropEnd ? _downloadParams.CropEndTime : _downloadParams.Video.Length;

            string fileName = _filenameService.SubstituteWildcards(currentPrefs.DownloadFileName, _downloadParams.Video,
                _downloadParams.SelectedQuality, cropStartTime, cropEndTime);
            fileName = _filenameService.EnsureExtension(fileName, currentPrefs.DownloadDisableConversion);

            _templateFilename = fileName;
            _downloadParams.Filename = fileName;
        }

        private async Task Download()
        {
            try
            {
                Validate();

                if (!HasErrors)
                {
                    if (File.Exists(_downloadParams.FullPath))
                    {
                        MessageBoxResult result = await _dialogService.ShowMessageBox(
                            "The file already exists. Do you want to overwrite it?", "Download",
                            MessageBoxButton.YesNo, MessageBoxImage.Question);

                        if (result != MessageBoxResult.Yes)
                        {
                            return;
                        }
                    }

                    _downloadService.Enqueue(_downloadParams);
                    _navigationService.NavigateBack();
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

            string currentProperty = nameof(DownloadParams);

            if (string.IsNullOrWhiteSpace(propertyName) || propertyName == currentProperty)
            {
                DownloadParams?.Validate();

                if (_downloadParams.FullPath.Length > 250)
                {
                    DownloadParams.AddError(nameof(DownloadParams.Filename),
                        "The full filename (directory + filename) must be shorter than 250 characters!");
                }

                if (_downloadService.IsFileNameUsed(_downloadParams.FullPath))
                {
                    DownloadParams.AddError(nameof(DownloadParams.Filename),
                        "Another video is already being downloaded to this file!");
                }

                if (DownloadParams.HasErrors)
                {
                    AddError(currentProperty, "Invalid Download Parameters!");

                    if (DownloadParams.GetErrors(nameof(DownloadParameters.CropStartTime)) is List<string>
                            cropStartErrors && cropStartErrors.Count > 0)
                    {
                        string firstError = cropStartErrors.First();
                        AddError(nameof(RangeStartText), firstError);
                    }

                    if (DownloadParams.GetErrors(nameof(DownloadParameters
                            .CropEndTime)) is List<string> cropEndErrors && cropEndErrors.Count > 0)
                    {
                        string firstError = cropEndErrors.First();
                        AddError(nameof(RangeEndText), firstError);
                    }
                }
            }
        }

        #endregion Methods

        #region EventHandlers

        private void DownloadParams_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DownloadParameters.EstimatedSizeStr))
            {
                FirePropertyChanged(nameof(RangeSummary));
            }

            // the user typed their own name: keep it until they press reset
            if (_downloadParams.Filename != _templateFilename)
            {
                return;
            }

            if (e.PropertyName == nameof(DownloadParameters.SelectedQuality)
                || e.PropertyName == nameof(DownloadParameters.CropStart)
                || e.PropertyName == nameof(DownloadParameters.CropEnd)
                || e.PropertyName == nameof(DownloadParameters.CropStartTime)
                || e.PropertyName == nameof(DownloadParameters.CropEndTime))
            {
                UpdateFilenameFromTemplate();
            }
        }

        #endregion EventHandlers
    }
}