using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using TwitchLeecher.Core.Enums;
using TwitchLeecher.Core.Models;

namespace TwitchLeecher.Services.Services
{
    // What downloads.json holds: the downloads still waiting, running or paused when the app was last open.
    // Playlist URLs and access tokens are not kept; they expire and are fetched again when a download starts.
    internal class SavedDownloadQueue
    {
        public int Version { get; set; } = 1;

        public List<SavedDownload> Downloads { get; set; } = new List<SavedDownload>();
    }

    internal class SavedDownload
    {
        public string Id { get; set; }

        // video
        public string VideoId { get; set; }
        public string Channel { get; set; }
        public string Title { get; set; }
        public int Views { get; set; }
        public TimeSpan Length { get; set; }
        public DateTime RecordedDate { get; set; }
        public Uri Thumbnail { get; set; }
        public Uri Url { get; set; }
        public bool Viewable { get; set; }
        public bool Muted { get; set; }
        public TimeSpan? StartTime { get; set; }
        public bool IsClip { get; set; }
        public string ClipCreator { get; set; }

        // selected quality
        public string QualityId { get; set; }
        public string QualityName { get; set; }
        public string QualityResolution { get; set; }
        public string QualityCodecs { get; set; }
        public long QualityBandwidth { get; set; }

        // what to do with it
        public string Folder { get; set; }
        public string Filename { get; set; }
        public bool DisableConversion { get; set; }
        public bool? Compress { get; set; }
        public bool DownloadChat { get; set; }
        public bool SaveVodInfo { get; set; }
        public ChatFormat ChatFormat { get; set; }
        public bool CropStart { get; set; }
        public TimeSpan CropStartTime { get; set; }
        public bool CropEnd { get; set; }
        public TimeSpan CropEndTime { get; set; }
        public bool FromWatchedSearch { get; set; }
        public bool WaitForSchedule { get; set; }

        public static SavedDownload From(TwitchVideoDownload download)
        {
            DownloadParameters p = download.DownloadParams;
            TwitchVideo v = p.Video;
            TwitchVideoQuality q = p.SelectedQuality;

            return new SavedDownload
            {
                Id = download.Id,
                VideoId = v.Id,
                Channel = v.Channel,
                Title = v.Title,
                Views = v.Views,
                Length = v.Length,
                RecordedDate = v.RecordedDate,
                Thumbnail = v.ThumbnailUri,
                Url = v.Url,
                Viewable = v.Viewable,
                Muted = v.Muted,
                StartTime = v.StartTime,
                IsClip = v.IsClip,
                ClipCreator = v.ClipCreator,
                QualityId = q.Id,
                QualityName = q.Name,
                QualityResolution = q.Resolution,
                QualityCodecs = q.Codecs,
                QualityBandwidth = q.Bandwidth,
                Folder = p.Folder,
                Filename = p.Filename,
                DisableConversion = p.DisableConversion,
                Compress = p.Compress,
                DownloadChat = p.DownloadChat,
                SaveVodInfo = p.SaveVodInfo,
                ChatFormat = p.ChatFormat,
                CropStart = p.CropStart,
                CropStartTime = p.CropStartTime,
                CropEnd = p.CropEnd,
                CropEndTime = p.CropEndTime,
                FromWatchedSearch = p.FromWatchedSearch,
                WaitForSchedule = p.WaitForSchedule
            };
        }

        // The id names the temp folder, so a file that was edited by hand must not smuggle in a path.
        public bool HasValidId => Guid.TryParseExact(Id, "N", out _);

        public TwitchVideoDownload ToDownload()
        {
            TwitchVideo video = new TwitchVideo(Channel, Title, VideoId, Views, Length, RecordedDate, Thumbnail, Url,
                Viewable, Muted, false)
            {
                StartTime = StartTime,
                IsClip = IsClip,
                ClipCreator = ClipCreator
            };

            TwitchVideoQuality quality = new TwitchVideoQuality(QualityId, QualityName, QualityResolution,
                QualityCodecs, QualityBandwidth);

            DownloadParameters p = new DownloadParameters(video, new List<TwitchVideoQuality> { quality }, quality,
                Folder, Filename, DisableConversion)
            {
                Compress = Compress,
                DownloadChat = DownloadChat,
                SaveVodInfo = SaveVodInfo,
                ChatFormat = ChatFormat,
                CropStart = CropStart,
                CropStartTime = CropStartTime,
                CropEnd = CropEnd,
                CropEndTime = CropEndTime,
                FromWatchedSearch = FromWatchedSearch,
                WaitForSchedule = WaitForSchedule
            };

            return new TwitchVideoDownload(p, Id);
        }
    }

    // Compile-time JSON code (the Native AOT build has no reflection-based serializer).
    [JsonSourceGenerationOptions(WriteIndented = true, UseStringEnumConverter = true)]
    [JsonSerializable(typeof(SavedDownloadQueue))]
    internal partial class SavedDownloadQueueJson : JsonSerializerContext
    {
    }
}
