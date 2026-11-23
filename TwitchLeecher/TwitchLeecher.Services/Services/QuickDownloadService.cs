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

        public DownloadParameters CreateDownloadParams(TwitchVideo video, out string problem, DefaultQuality quality = null)
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
                    problem = "This video is for subscribers only.";
                    return null;
                }

                playlistInfo = _apiService.GetPlaylistInfo(video.Id, vodAuthInfo);
            }

            List<TwitchVideoQuality> qualities = playlistInfo.Keys.OrderBy(q => q).ToList();

            Preferences currentPrefs = _preferencesService.CurrentPreferences.Clone();

            TwitchVideoQuality selectedQuality = (quality ?? currentPrefs.DownloadDefaultQuality).PickFrom(qualities);

            (string folder, string filename, bool disableConversion) = GetDefaultTarget(video, currentPrefs, selectedQuality);

            DownloadParameters downloadParams = new DownloadParameters(video, qualities,
                selectedQuality, folder, filename, disableConversion);

            if (video.StartTime.HasValue)
            {
                downloadParams.CropStartTime = video.StartTime.Value;
            }

            problem = null;
            return downloadParams;
        }

        public string TryEnqueue(DownloadParameters downloadParams, bool startPaused = false)
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

            _downloadService.Enqueue(downloadParams, startPaused);
            return null;
        }

        public string GetDefaultPath(TwitchVideo video, TwitchVideoQuality quality)
        {
            (string folder, string filename, _) = GetDefaultTarget(video, _preferencesService.CurrentPreferences, quality);
            return Path.Combine(folder, filename);
        }

        private (string folder, string filename, bool disableConversion) GetDefaultTarget(TwitchVideo video,
            Preferences prefs, TwitchVideoQuality quality)
        {
            string folder = prefs.DownloadSubfoldersForFav &&
                            _preferencesService.IsChannelInFavourites(video.Channel)
                ? Path.Combine(prefs.DownloadFolder, video.Channel)
                : prefs.DownloadFolder;

            // clips are always a ready-made mp4
            bool disableConversion = prefs.DownloadDisableConversion && !video.IsClip;

            string filename = _filenameService.SubstituteWildcards(prefs.DownloadFileName, video, quality);
            filename = _filenameService.EnsureExtension(filename, disableConversion);

            return (folder, filename, disableConversion);
        }

        #endregion Methods
    }
}
