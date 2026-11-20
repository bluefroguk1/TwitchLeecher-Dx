using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using TwitchLeecher.Core.Enums;
using TwitchLeecher.Shared.Helpers;
using TwitchLeecher.Shared.IO;
using TwitchLeecher.Shared.Notification;

namespace TwitchLeecher.Core.Models
{
    public class Preferences : BindableBase
    {
        #region Static Fields

        public static List<DefaultQuality> DefaultQualities = new List<DefaultQuality>()
        {
            new DefaultQuality(DefaultQuality.SOURCE_RES, "Source"),
            new DefaultQuality(720, "720p"),
            new DefaultQuality(480, "480p"),
            new DefaultQuality(360, "360p"),
            new DefaultQuality(160, "160p"),
            new DefaultQuality(DefaultQuality.AUDIO_ONLY_RES, "Audio Only")
        };

        #endregion Static Fields

        #region Fields

        private Version _version;

        private bool _appCheckForUpdates;

        private bool _appShowDonationButton;

        private bool _appMinimiseToTray;

        private bool _appTrayPromptShown;

        private bool _appStartWithOs;

        private bool _appStartMinimised;

        private bool _appShowNotifications;

        private RangeObservableCollection<string> _searchFavouriteChannels;

        private string _searchChannelName;

        private VideoType _searchVideoType;

        private LoadLimitType _searchLoadLimitType;

        private int _searchLoadLastDays;

        private int _searchLoadLastVods;

        private bool _searchOnStartup;

        private bool _searchDefaultFavourites;

        private string _downloadTempFolder;

        private string _downloadFolder;

        private string _downloadFileName;

        private DefaultQuality _downloadDefaultQuality;

        private bool _downloadSubfoldersForFav;

        private bool _downloadRemoveCompleted;

        private bool _downloadDisableConversion;

        private bool _downloadQuickDownload;

        private bool _scheduleEnabled;

        private int _scheduleStartHour = 1;

        private int _scheduleEndHour = 7;

        private bool _scheduleIncludePaused = true;

        private bool _schedulePauseAtEnd = true;

        private bool _autoDownloadEnabled;

        private int _autoDownloadInterval;

        private string _autoDownloadChannels;

        private bool _autoDownloadClips;

        private int _autoDownloadClipMinViews;

        private bool _downloadCompress;

        private CompressionLevel _downloadCompressLevel = CompressionLevel.Balanced;

        private bool _miscUseExternalPlayer;

        private string _miscExternalPlayer;
        private string _theme;
        private int _maxConnectionCount;

        #endregion Fields

        #region Properties

        public Version Version
        {
            get { return _version; }
            set { SetProperty(ref _version, value); }
        }

        public bool AppCheckForUpdates
        {
            get { return _appCheckForUpdates; }
            set { SetProperty(ref _appCheckForUpdates, value); }
        }

        public bool AppShowDonationButton
        {
            get { return _appShowDonationButton; }
            set { SetProperty(ref _appShowDonationButton, value); }
        }

        public bool AppMinimiseToTray
        {
            get { return _appMinimiseToTray; }
            set { SetProperty(ref _appMinimiseToTray, value); }
        }

        public bool AppTrayPromptShown
        {
            get { return _appTrayPromptShown; }
            set { SetProperty(ref _appTrayPromptShown, value); }
        }

        public bool AppStartWithOs
        {
            get { return _appStartWithOs; }
            set { SetProperty(ref _appStartWithOs, value); }
        }

        public bool AppStartMinimised
        {
            get { return _appStartMinimised; }
            set { SetProperty(ref _appStartMinimised, value); }
        }

        public bool AppShowNotifications
        {
            get { return _appShowNotifications; }
            set { SetProperty(ref _appShowNotifications, value); }
        }

        public bool MiscUseExternalPlayer
        {
            get { return _miscUseExternalPlayer; }
            set { SetProperty(ref _miscUseExternalPlayer, value); }
        }

        public string MiscExternalPlayer
        {
            get { return _miscExternalPlayer; }

            set { SetProperty(ref _miscExternalPlayer, value); }
        }

        public string Theme
        {
            get { return _theme; }
            set { SetProperty(ref _theme, value); }
        }

        public RangeObservableCollection<string> SearchFavouriteChannels
        {
            get
            {
                if (_searchFavouriteChannels == null)
                {
                    _searchFavouriteChannels = new RangeObservableCollection<string>();
                }

                return _searchFavouriteChannels;
            }
        }

        public string SearchChannelName
        {
            get { return _searchChannelName; }
            set { SetProperty(ref _searchChannelName, value); }
        }

        public VideoType SearchVideoType
        {
            get { return _searchVideoType; }
            set { SetProperty(ref _searchVideoType, value); }
        }

        public LoadLimitType SearchLoadLimitType
        {
            get { return _searchLoadLimitType; }
            set { SetProperty(ref _searchLoadLimitType, value); }
        }

        public int SearchLoadLastDays
        {
            get { return _searchLoadLastDays; }
            set { SetProperty(ref _searchLoadLastDays, value); }
        }

        public int SearchLoadLastVods
        {
            get { return _searchLoadLastVods; }
            set { SetProperty(ref _searchLoadLastVods, value); }
        }

        public bool SearchOnStartup
        {
            get { return _searchOnStartup; }
            set { SetProperty(ref _searchOnStartup, value); }
        }

        // start the search page (and 'Search on Start') on the Favourites search type (#86)
        public bool SearchDefaultFavourites
        {
            get { return _searchDefaultFavourites; }
            set { SetProperty(ref _searchDefaultFavourites, value); }
        }

        public string DownloadTempFolder
        {
            get { return _downloadTempFolder; }
            set { SetProperty(ref _downloadTempFolder, value); }
        }

        public string DownloadFolder
        {
            get { return _downloadFolder; }
            set { SetProperty(ref _downloadFolder, value); }
        }

        public string DownloadFileName
        {
            get { return _downloadFileName; }
            set { SetProperty(ref _downloadFileName, value); }
        }

        public DefaultQuality DownloadDefaultQuality
        {
            get { return _downloadDefaultQuality; }
            set { SetProperty(ref _downloadDefaultQuality, value); }
        }

        public List<DefaultQuality> DownloadDefaultQualities
        {
            get { return DefaultQualities; }
        }

        public bool DownloadSubfoldersForFav
        {
            get { return _downloadSubfoldersForFav; }
            set { SetProperty(ref _downloadSubfoldersForFav, value); }
        }

        public bool DownloadRemoveCompleted
        {
            get { return _downloadRemoveCompleted; }
            set { SetProperty(ref _downloadRemoveCompleted, value); }
        }

        public bool DownloadDisableConversion
        {
            get { return _downloadDisableConversion; }
            set { SetProperty(ref _downloadDisableConversion, value); }
        }

        // off-peak schedule: watched-search finds wait for this window (see DownloadSchedule)
        public bool ScheduleEnabled
        {
            get { return _scheduleEnabled; }
            set { SetProperty(ref _scheduleEnabled, value); }
        }

        public int ScheduleStartHour
        {
            get { return _scheduleStartHour; }
            set { SetProperty(ref _scheduleStartHour, Math.Clamp(value, 0, 23)); }
        }

        public int ScheduleEndHour
        {
            get { return _scheduleEndHour; }
            set { SetProperty(ref _scheduleEndHour, Math.Clamp(value, 0, 23)); }
        }

        // also start downloads the user paused when the window opens
        public bool ScheduleIncludePaused
        {
            get { return _scheduleIncludePaused; }
            set { SetProperty(ref _scheduleIncludePaused, value); }
        }

        // pause what the schedule started when the window closes
        public bool SchedulePauseAtEnd
        {
            get { return _schedulePauseAtEnd; }
            set { SetProperty(ref _schedulePauseAtEnd, value); }
        }

        public bool DownloadQuickDownload
        {
            get { return _downloadQuickDownload; }
            set { SetProperty(ref _downloadQuickDownload, value); }
        }

        public bool AutoDownloadEnabled
        {
            get { return _autoDownloadEnabled; }
            set { SetProperty(ref _autoDownloadEnabled, value); }
        }

        // minutes between channel checks
        public int AutoDownloadInterval
        {
            get { return _autoDownloadInterval; }
            set { SetProperty(ref _autoDownloadInterval, value); }
        }

        // channel names separated by commas, semicolons or spaces
        public string AutoDownloadChannels
        {
            get { return _autoDownloadChannels; }
            set { SetProperty(ref _autoDownloadChannels, value); }
        }

        public bool AutoDownloadClips
        {
            get { return _autoDownloadClips; }
            set { SetProperty(ref _autoDownloadClips, value); }
        }

        public int AutoDownloadClipMinViews
        {
            get { return _autoDownloadClipMinViews; }
            set { SetProperty(ref _autoDownloadClipMinViews, value); }
        }

        public string[] GetAutoDownloadChannels()
        {
            return (_autoDownloadChannels ?? string.Empty)
                .Split(new[] { ',', ';', ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        // re-encode finished downloads to make them smaller (default for the download dialog and quick download)
        public bool DownloadCompress
        {
            get { return _downloadCompress; }
            set { SetProperty(ref _downloadCompress, value); }
        }

        public CompressionLevel DownloadCompressLevel
        {
            get { return _downloadCompressLevel; }
            set { SetProperty(ref _downloadCompressLevel, value); }
        }

        // the Settings dropdown lists the levels in enum order
        public int DownloadCompressLevelIndex
        {
            get { return (int)_downloadCompressLevel; }
            set
            {
                if (value >= 0)
                {
                    DownloadCompressLevel = (CompressionLevel)value;
                    FirePropertyChanged(nameof(DownloadCompressLevelIndex));
                }
            }
        }

        public int MaxConnectionCount
        {
            get { return _maxConnectionCount; }
            set { SetProperty(ref _maxConnectionCount, value); }
        }

        #endregion Properties

        #region Methods

        public override void Validate(string propertyName = null)
        {
            base.Validate(propertyName);

            string currentProperty = nameof(MiscExternalPlayer);

            if (string.IsNullOrWhiteSpace(propertyName) || propertyName == currentProperty)
            {
                if (MiscUseExternalPlayer)
                {
                    if (string.IsNullOrWhiteSpace(_miscExternalPlayer))
                    {
                        AddError(currentProperty, "Please specify an external player!");
                    }
                    // skip executability check for linux: no built-in functionality to check permissions
                    else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) 
                             && !_miscExternalPlayer.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    {
                        AddError(currentProperty, "File must be an executable!");
                    }
                    else if (!File.Exists(_miscExternalPlayer))
                    {
                        AddError(currentProperty, "The specified file does not exist!");
                    }
                }
            }

            currentProperty = nameof(SearchChannelName);

            if (string.IsNullOrWhiteSpace(propertyName) || propertyName == currentProperty)
            {
                if (_searchOnStartup && !_searchDefaultFavourites && string.IsNullOrWhiteSpace(_searchChannelName))
                {
                    AddError(currentProperty,
                        "If 'Search on Startup' is enabled, you need to specify a default channel name!");
                }
            }

            currentProperty = nameof(SearchLoadLastDays);

            if (string.IsNullOrWhiteSpace(propertyName) || propertyName == currentProperty)
            {
                if (_searchLoadLimitType == LoadLimitType.Timespan &&
                    (_searchLoadLastDays < 1 || _searchLoadLastDays > 999))
                {
                    AddError(currentProperty, "Value has to be between 1 and 999!");
                }
            }

            currentProperty = nameof(SearchLoadLastVods);

            if (string.IsNullOrWhiteSpace(propertyName) || propertyName == currentProperty)
            {
                if (_searchLoadLimitType == LoadLimitType.LastVods &&
                    (_searchLoadLastVods < 1 || _searchLoadLastVods > 999))
                {
                    AddError(currentProperty, "Value has to be between 1 and 999!");
                }
            }

            currentProperty = nameof(AutoDownloadInterval);

            if (string.IsNullOrWhiteSpace(propertyName) || propertyName == currentProperty)
            {
                if (_autoDownloadInterval < 5 || _autoDownloadInterval > 1440)
                {
                    AddError(currentProperty, "Value has to be between 5 and 1440!");
                }
            }

            currentProperty = nameof(AutoDownloadClipMinViews);

            if (string.IsNullOrWhiteSpace(propertyName) || propertyName == currentProperty)
            {
                if (_autoDownloadClipMinViews < 0)
                {
                    AddError(currentProperty, "Value cannot be negative!");
                }
            }

            currentProperty = nameof(DownloadTempFolder);

            if (string.IsNullOrWhiteSpace(propertyName) || propertyName == currentProperty)
            {
                if (string.IsNullOrWhiteSpace(DownloadTempFolder))
                {
                    AddError(currentProperty, "Please specify a temporary download folder!");
                }

                // Assume max length of .ts filename in playlist is '99999999.ts' => 11 characters
                if (DownloadTempFolder.Length + 11 > 250)
                {
                    AddError(currentProperty, "The length of this folder path must be shorter than 239 characters!");
                }
            }

            currentProperty = nameof(DownloadFolder);

            if (string.IsNullOrWhiteSpace(propertyName) || propertyName == currentProperty)
            {
                if (string.IsNullOrWhiteSpace(_downloadFolder))
                {
                    AddError(currentProperty, "Please specify a default download folder!");
                }
            }

            currentProperty = nameof(DownloadFileName);

            if (string.IsNullOrWhiteSpace(propertyName) || propertyName == currentProperty)
            {
                if (string.IsNullOrWhiteSpace(_downloadFileName))
                {
                    AddError(currentProperty, "Please specify a default download filename!");
                }
                else if (FileSystem.FilenameContainsInvalidChars(_downloadFileName))
                {
                    AddError(currentProperty, $"Filename contains invalid characters ({FileSystem.InvalidFilenameChars}.)!");
                }
            }
        }

        public Preferences Clone()
        {
            Preferences clone = new Preferences()
            {
                Version = Version,
                AppCheckForUpdates = AppCheckForUpdates,
                AppShowDonationButton = AppShowDonationButton,
                AppMinimiseToTray = AppMinimiseToTray,
                AppTrayPromptShown = AppTrayPromptShown,
                AppStartWithOs = AppStartWithOs,
                AppStartMinimised = AppStartMinimised,
                AppShowNotifications = AppShowNotifications,
                MiscUseExternalPlayer = MiscUseExternalPlayer,
                MiscExternalPlayer = MiscExternalPlayer,
                SearchChannelName = SearchChannelName,
                SearchVideoType = SearchVideoType,
                SearchLoadLimitType = SearchLoadLimitType,
                SearchLoadLastDays = SearchLoadLastDays,
                SearchLoadLastVods = SearchLoadLastVods,
                SearchOnStartup = SearchOnStartup,
                SearchDefaultFavourites = SearchDefaultFavourites,
                DownloadTempFolder = DownloadTempFolder,
                DownloadFolder = DownloadFolder,
                DownloadFileName = DownloadFileName,
                DownloadDefaultQuality = _downloadDefaultQuality,
                DownloadSubfoldersForFav = DownloadSubfoldersForFav,
                DownloadRemoveCompleted = DownloadRemoveCompleted,
                DownloadDisableConversion = DownloadDisableConversion,
                DownloadQuickDownload = DownloadQuickDownload,
                ScheduleEnabled = ScheduleEnabled,
                ScheduleStartHour = ScheduleStartHour,
                ScheduleEndHour = ScheduleEndHour,
                ScheduleIncludePaused = ScheduleIncludePaused,
                SchedulePauseAtEnd = SchedulePauseAtEnd,
                AutoDownloadEnabled = AutoDownloadEnabled,
                AutoDownloadInterval = AutoDownloadInterval,
                AutoDownloadChannels = AutoDownloadChannels,
                AutoDownloadClips = AutoDownloadClips,
                AutoDownloadClipMinViews = AutoDownloadClipMinViews,
                DownloadCompress = DownloadCompress,
                DownloadCompressLevel = DownloadCompressLevel,
                Theme = Theme,
                MaxConnectionCount = MaxConnectionCount
            };

            clone.SearchFavouriteChannels.AddRange(SearchFavouriteChannels);

            return clone;
        }

        #endregion Methods
    }
}