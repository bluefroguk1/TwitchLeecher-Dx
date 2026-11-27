using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using TwitchLeecher.Shared.Extensions;
using TwitchLeecher.Shared.Notification;

namespace TwitchLeecher.Core.Models
{
    public class TwitchVideo : BindableBase
    {
        #region Constatnts

        private const string UNTITLED_BROADCAST = "Untitled Broadcast";

        #endregion Constatnts

        #region Constructors

        public TwitchVideo(string channel, string title, string id, int views, TimeSpan length, DateTime recordedDate, Uri thumbnail, Uri url, bool viewable, bool muted, bool live)
        {
            if (string.IsNullOrWhiteSpace(channel))
            {
                throw new ArgumentNullException(nameof(channel));
            }

            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            if (string.IsNullOrWhiteSpace(title))
            {
                title = UNTITLED_BROADCAST;
            }

            Channel = channel;
            Title = title;
            Id = id;
            Views = views;
            Length = length;
            RecordedDate = recordedDate;
            // Created on first access, i.e. when a card is realised and binds to it, so
            // off-screen results do not download their thumbnails up front.
            _thumbnail = new Lazy<Task<Bitmap>>(() => ImageHelper.LoadFromWeb(thumbnail));
            Url = url;
            Muted = muted;
            Live = live;
        }

        #endregion Constructors

        #region Properties

        private bool _isSelected;

        // ticked in the search results for "Download Selected" (#96)
        public bool IsSelected
        {
            get { return _isSelected; }
            set { SetProperty(ref _isSelected, value); }
        }

        private bool _isQueued;

        // search results badges, set by the results view model
        public bool IsQueued
        {
            get { return _isQueued; }
            set { SetProperty(ref _isQueued, value); }
        }

        private bool _isDownloaded;

        public bool IsDownloaded
        {
            get { return _isDownloaded; }
            set { SetProperty(ref _isDownloaded, value); }
        }

        // Set by the API service for VODs: asks Twitch whether the VOD is locked for the current login
        // (sub-only and the user may not watch all of it). Run once, when a card first shows the badge.
        public Func<Task<bool>> LockedCheck { get; init; }

        private bool _isLocked;
        private int _lockedChecked;

        // true only when the check says so; a slow or failed check shows nothing
        public bool IsLocked
        {
            get
            {
                if (LockedCheck != null && Interlocked.Exchange(ref _lockedChecked, 1) == 0)
                {
                    _ = LoadLockedAsync();
                }

                return _isLocked;
            }
        }

        private async Task LoadLockedAsync()
        {
            if (await LockedCheck())
            {
                _isLocked = true;
                FirePropertyChanged(nameof(IsLocked));
            }
        }

        // "Clip by X · 9 Nov · 1.8K views · muted parts"
        public string MetaStr
        {
            get
            {
                string views = Views >= 1_000_000 ? (Views / 1_000_000.0).ToString("0.#") + "M"
                    : Views >= 1_000 ? (Views / 1_000.0).ToString("0.#") + "K"
                    : Views.ToString();
                string meta = RecordedDate.ToString("d MMM") + " · " + views + " views";

                if (IsClip && !string.IsNullOrWhiteSpace(ClipCreator))
                {
                    meta = "Clip by " + ClipCreator + " · " + meta;
                }

                return Muted ? meta + " · muted parts" : meta;
            }
        }

        public string Channel { get; }

        public string Title { get; }

        public string Id { get; }

        public TimeSpan Length { get; }

        public string LengthStr
        {
            get
            {
                return Length.ToDaylessString();
            }
        }

        public int Views { get; }

        public DateTime RecordedDate { get; }

        private readonly Lazy<Task<Bitmap>> _thumbnail;

        public Task<Bitmap> Thumbnail => _thumbnail.Value;

        public Uri Url { get; }

        public bool Viewable { get; }

        public bool Muted { get; }

        public bool Live { get; }

        public TimeSpan? StartTime { get; set; }

        // Clips are a single mp4 identified by their slug (Id), not a VOD playlist (#37)
        public bool IsClip { get; init; }

        // who clipped it, empty for VODs
        public string ClipCreator { get; init; }

        #endregion Properties
    }
}