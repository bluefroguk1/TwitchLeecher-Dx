using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Threading;
using TwitchLeecher.Core.Enums;
using TwitchLeecher.Core.Models;
using TwitchLeecher.Gui.Interfaces;
using TwitchLeecher.Services.Interfaces;

namespace TwitchLeecher.Gui.Services
{
    internal class ScheduleService : IScheduleService
    {
        private readonly IDownloadService _downloadService;
        private readonly IPreferencesService _preferencesService;

        // downloads this service started, so only those are paused again when the window closes
        private readonly HashSet<string> _startedBySchedule = new HashSet<string>();

        private DispatcherTimer _timer;
        private bool? _wasOpen;

        public ScheduleService(IDownloadService downloadService, IPreferencesService preferencesService)
        {
            _downloadService = downloadService;
            _preferencesService = preferencesService;
        }

        public void Start()
        {
            if (_timer != null)
            {
                return;
            }

            // the window is whole hours, so a check every 30 s is precise enough
            _timer = new DispatcherTimer(TimeSpan.FromSeconds(30), DispatcherPriority.Background, (_, _) => Tick());
            _timer.Start();
            Tick();
        }

        private void Tick()
        {
            Preferences prefs = _preferencesService.CurrentPreferences;

            if (!prefs.ScheduleEnabled)
            {
                // nothing to do, and a later switch-on must not see a stale "was open"
                _wasOpen = null;
                _startedBySchedule.Clear();
                return;
            }

            bool open = DownloadSchedule.IsOpen(prefs, DateTime.Now);

            if (open && _wasOpen != true)
            {
                // window opened (or the app started inside it)
                foreach (TwitchVideoDownload download in _downloadService.Downloads
                             .Where(d => d.DownloadState == DownloadState.Paused &&
                                         (d.DownloadParams.WaitForSchedule || prefs.ScheduleIncludePaused))
                             .ToList())
                {
                    _startedBySchedule.Add(download.Id);
                    _downloadService.Resume(download.Id);
                }
            }
            else if (!open && _wasOpen == true && prefs.SchedulePauseAtEnd)
            {
                // window closed: hold what the schedule started, it carries on next time
                foreach (TwitchVideoDownload download in _downloadService.Downloads
                             .Where(d => _startedBySchedule.Contains(d.Id) &&
                                         (d.DownloadState == DownloadState.Queued ||
                                          d.DownloadState == DownloadState.Downloading))
                             .ToList())
                {
                    download.DownloadParams.WaitForSchedule = true;
                    _downloadService.Pause(download.Id);
                }

                _startedBySchedule.Clear();
            }

            _wasOpen = open;
        }
    }
}
