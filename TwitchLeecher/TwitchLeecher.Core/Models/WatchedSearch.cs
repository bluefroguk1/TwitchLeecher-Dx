using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using TwitchLeecher.Core.Enums;

namespace TwitchLeecher.Core.Models
{
    // A saved channel search that is re-run every few minutes; new results are queued.
    // Plain INotifyPropertyChanged (not BindableBase): it is saved with System.Text.Json and
    // BindableBase's notification members do not serialise.
    public class WatchedSearch : INotifyPropertyChanged
    {
        #region Constants

        public const int DEFAULT_QUALITY = -1;

        #endregion Constants

        #region Fields

        private string _channel = string.Empty;
        private VideoType _videoType = VideoType.Broadcast;
        private int _minViews = 100;
        private int _quality = DEFAULT_QUALITY;
        private bool _active = true;
        private bool _startPaused;
        private string _latestResult;
        private bool _isEditing;

        #endregion Fields

        #region Properties

        public string Channel
        {
            get => _channel;
            set => Set(ref _channel, value?.Trim() ?? string.Empty);
        }

        public VideoType VideoType
        {
            get => _videoType;
            set => Set(ref _videoType, value);
        }

        // Clips only: a clip is queued once it has at least this many views.
        public int MinViews
        {
            get => _minViews;
            set => Set(ref _minViews, value < 0 ? 0 : value);
        }

        // Vertical resolution from Preferences.DefaultQualities, or DEFAULT_QUALITY for the download default.
        public int Quality
        {
            get => _quality;
            set => Set(ref _quality, value);
        }

        public bool Active
        {
            get => _active;
            set => Set(ref _active, value);
        }

        // "When found": false = download now, true = add to the queue paused.
        public bool StartPaused
        {
            get => _startPaused;
            set => Set(ref _startPaused, value);
        }

        // The seen-list key. Matches the keys the old channel list used ("name" and "name#clips"),
        // so migrated searches keep their history and do not run a first check again.
        [JsonIgnore]
        public string Key
        {
            get
            {
                string channel = Channel.ToLowerInvariant();

                switch (VideoType)
                {
                    case VideoType.Clip: return channel + "#clips";
                    case VideoType.Highlight: return channel + "#highlights";
                    case VideoType.Upload: return channel + "#uploads";
                    default: return channel;
                }
            }
        }

        [JsonIgnore]
        public string TypeName
        {
            get
            {
                switch (VideoType)
                {
                    case VideoType.Clip: return "Clips";
                    case VideoType.Highlight: return "Highlights";
                    case VideoType.Upload: return "Uploads";
                    default: return "Broadcasts";
                }
            }
        }

        [JsonIgnore]
        public string Label => (string.IsNullOrEmpty(Channel) ? "New search" : Channel) + " · " + TypeName;

        [JsonIgnore]
        public bool IsClip => VideoType == VideoType.Clip;

        [JsonIgnore]
        public string QualityName
        {
            get
            {
                DefaultQuality quality = GetQuality();

                if (quality == null)
                {
                    return "Default quality";
                }

                return quality.IsSource ? "Source quality" : quality.DisplayString;
            }
        }

        [JsonIgnore]
        public IEnumerable<string> Tags
        {
            get
            {
                if (IsClip)
                {
                    yield return "≥ " + MinViews + " views";
                }

                yield return QualityName;
            }
        }

        // ComboBox helpers: enum order is Broadcast, Highlight, Upload, Clip, the same as the items in the view.
        [JsonIgnore]
        public int VideoTypeIndex
        {
            get => (int)VideoType;
            set => VideoType = value < 0 ? VideoType.Broadcast : (VideoType)value;
        }

        [JsonIgnore]
        public int WhenFoundIndex
        {
            get => StartPaused ? 1 : 0;
            set => StartPaused = value == 1;
        }

        // 0 = default quality, then Preferences.DefaultQualities in order.
        [JsonIgnore]
        public int QualityIndex
        {
            get => Preferences.DefaultQualities.FindIndex(q => q.VerticalResolution == Quality) + 1;
            set => Quality = value <= 0 || value > Preferences.DefaultQualities.Count
                ? DEFAULT_QUALITY
                : Preferences.DefaultQualities[value - 1].VerticalResolution;
        }

        [JsonIgnore]
        public string LatestResult
        {
            get => _latestResult;
            set => Set(ref _latestResult, value);
        }

        [JsonIgnore]
        public bool IsEditing
        {
            get => _isEditing;
            set => Set(ref _isEditing, value);
        }

        #endregion Properties

        #region Methods

        // null means "use the download default from Settings".
        public DefaultQuality GetQuality()
        {
            return Quality == DEFAULT_QUALITY
                ? null
                : Preferences.DefaultQualities.Find(q => q.VerticalResolution == Quality);
        }

        #endregion Methods

        #region Events

        public event PropertyChangedEventHandler PropertyChanged;

        private void Set<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
        {
            if (Equals(storage, value))
            {
                return;
            }

            storage = value;
            Fire(propertyName);

            // Display properties that depend on the saved ones.
            switch (propertyName)
            {
                case nameof(Channel):
                    Fire(nameof(Label));
                    break;
                case nameof(VideoType):
                    Fire(nameof(Label));
                    Fire(nameof(TypeName));
                    Fire(nameof(IsClip));
                    Fire(nameof(Tags));
                    Fire(nameof(VideoTypeIndex));
                    break;
                case nameof(MinViews):
                    Fire(nameof(Tags));
                    break;
                case nameof(Quality):
                    Fire(nameof(QualityName));
                    Fire(nameof(Tags));
                    Fire(nameof(QualityIndex));
                    break;
                case nameof(StartPaused):
                    Fire(nameof(WhenFoundIndex));
                    break;
            }
        }

        private void Fire(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion Events
    }
}
