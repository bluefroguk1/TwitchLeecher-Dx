using System.Collections.Generic;
using System.IO;
using System.Linq;
using TwitchLeecher.Core.Models;
using TwitchLeecher.Services.Interfaces;

namespace TwitchLeecher.Services.Services
{
    internal class QuickDownloadService : IQuickDownloadService
    {
        #region Fields

        private readonly IApiService _apiService;
        private readonly IAuthService _authService;
        private readonly IPreferencesService _preferencesService;
        private readonly IFilenameService _filenameService;
        private readonly IDownloadService _downloadService;

        #endregion Fields

        #region Constructors

        public QuickDownloadService(
            IApiService apiService,
            IAuthService authService,
            IPreferencesService preferencesService,
            IFilenameService filenameService,
            IDownloadService downloadService)
        {
            _apiService = apiService;
            _authService = authService;
            _preferencesService = preferencesService;
            _filenameService = filenameService;
            _downloadService = downloadService;
        }

        #endregion Constructors

        #region Methods

        public DownloadParameters CreateDownloadParams(TwitchVideo video, out string problem)
        {
            Dictionary<TwitchVideoQuality, string> playlistInfo;

            if (video.IsClip)
            {
                playlistInfo = _apiService.GetClipQualities(video.Id);
            }
            else
            {
                TwitchVideoAuthInfo vodAuthInfo = _apiService.GetVodAuthInfo(video.Id);

                if (!vodAuthInfo.Privileged && vodAuthInfo.SubOnly)
                {
                    problem = _authService.IsAuthenticatedSubOnly
                        ? "This video is sub-only but you are not subscribed to the channel!"
                        : "This video is sub-only! You need to enable sub-only video download support first!";
                    return null;
                }

                playlistInfo = _apiService.GetPlaylistInfo(video.Id, vodAuthInfo);
            }

            List<TwitchVideoQuality> qualities = playlistInfo.Keys.OrderBy(q => q).ToList();

            Preferences currentPrefs = _preferencesService.CurrentPreferences.Clone();

            TwitchVideoQuality selectedQuality = GetSelectedQuality(qualities, currentPrefs.DownloadDefaultQuality);

            string folder = currentPrefs.DownloadSubfoldersForFav &&
                            _preferencesService.IsChannelInFavourites(video.Channel)
                ? Path.Combine(currentPrefs.DownloadFolder, video.Channel)
                : currentPrefs.DownloadFolder;

            // clips are always a ready-made mp4
            bool disableConversion = currentPrefs.DownloadDisableConversion && !video.IsClip;

            string filename = _filenameService.SubstituteWildcards(currentPrefs.DownloadFileName, video,
                selectedQuality);
            filename = _filenameService.EnsureExtension(filename, disableConversion);

            DownloadParameters downloadParams = new DownloadParameters(video, qualities,
                selectedQuality, folder, filename, disableConversion);

            if (video.StartTime.HasValue)
            {
                downloadParams.CropStartTime = video.StartTime.Value;
            }

            problem = null;
            return downloadParams;
        }

        public string TryEnqueue(DownloadParameters downloadParams)
        {
            downloadParams.Validate();

            if (downloadParams.HasErrors)
            {
                return "Invalid download settings, check Settings > Download";
            }

            if (_downloadService.IsFileNameUsed(downloadParams.FullPath))
            {
                return "Already in the download queue";
            }

            if (File.Exists(downloadParams.FullPath))
            {
                return "File already exists";
            }

            _downloadService.Enqueue(downloadParams);
            return null;
        }

        private TwitchVideoQuality GetSelectedQuality(List<TwitchVideoQuality> qualities, DefaultQuality defaultQuality)
        {
            TwitchVideoQuality sourceQuality = qualities.Find(q => q.IsSource);

            if (sourceQuality == null)
            {
                sourceQuality = qualities[0];
            }

            if (defaultQuality.IsSource)
            {
                return sourceQuality;
            }

            if (defaultQuality.IsAudioOnly)
            {
                TwitchVideoQuality audioOnlyQuality = qualities.FirstOrDefault(q => q.IsAudioOnly);

                if (audioOnlyQuality != null)
                {
                    return audioOnlyQuality;
                }
                else
                {
                    return sourceQuality;
                }
            }

            IEnumerable<TwitchVideoQuality> visualQualities = qualities.Where(q => !q.IsAudioOnly);

            int defaultRes = defaultQuality.VerticalResolution;

            TwitchVideoQuality selectedQuality = null;

            foreach (TwitchVideoQuality quality in visualQualities)
            {
                if (quality.VerticalResolution <= defaultRes && (selectedQuality == null ||
                                                                 selectedQuality.VerticalResolution <
                                                                 quality.VerticalResolution))
                {
                    selectedQuality = quality;
                }
            }

            if (selectedQuality != null)
            {
                return selectedQuality;
            }

            foreach (TwitchVideoQuality quality in visualQualities)
            {
                if (quality.VerticalResolution >= defaultRes && (selectedQuality == null ||
                                                                 selectedQuality.VerticalResolution >
                                                                 quality.VerticalResolution))
                {
                    selectedQuality = quality;
                }
            }

            if (selectedQuality != null)
            {
                return selectedQuality;
            }

            return sourceQuality;
        }

        #endregion Methods
    }
}
