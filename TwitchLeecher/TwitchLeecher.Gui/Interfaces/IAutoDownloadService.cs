using System.Collections.ObjectModel;
using TwitchLeecher.Core.Enums;
using TwitchLeecher.Core.Models;

namespace TwitchLeecher.Gui.Interfaces
{
    // One line of the watched searches log. Kind: STARTED, PAUSED, WAITING, SKIPPED, FIRST RUN or ERROR.
    public record WatchedActivity(DateTime Time, string Kind, string Search, string Message)
    {
        public string TimeText => Time.Date == DateTime.Today ? Time.ToString("HH:mm") : Time.ToString("d MMM");

        public string Text => Search + " · " + Message;

        public bool IsSuccess => Kind == "STARTED";

        public bool IsWarning => Kind is "WAITING" or "SKIPPED";

        public bool IsError => Kind == "ERROR";
    }

    // Watched searches (code name: auto download). Settings and searches are saved to their own file
    // as soon as they change, so they are not part of the Settings page's Save/Undo.
    public interface IAutoDownloadService
    {
        // Edit on the UI thread only; every change is saved straight away.
        ObservableCollection<WatchedSearch> Searches { get; }

        bool Enabled { get; set; }

        // Minutes between checks.
        int Interval { get; set; }

        DateTime? LastCheck { get; }

        // Raised on the UI thread after a check and after Enabled, Interval or the searches change.
        event EventHandler Changed;

        // Starts the background check loop. Safe to call more than once.
        void Start();

        // Runs every active search now, even while watched searches are switched off.
        void CheckNow();

        // Adds an active search with default settings. Returns the existing one if this channel and type are already watched.
        WatchedSearch Add(string channel, VideoType videoType);

        // Newest first.
        List<WatchedActivity> ReadActivity(int max);
    }
}
