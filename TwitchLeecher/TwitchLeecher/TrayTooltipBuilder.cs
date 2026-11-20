using System.Globalization;

namespace TwitchLeecher;

// Builds the tray icon tooltip text. Pure, so it can be tested without the UI.
public static class TrayTooltipBuilder
{
    public const string AppName = "Twitch Leecher DX";

    // downloadingCount / averageLeftPercent: items downloading now and the mean of (100 - progress) over them;
    // pausedCount: paused items; watchedSearchCount: active watched searches (0 when watching is off).
    public static string Build(int downloadingCount, double averageLeftPercent, int pausedCount, int watchedSearchCount)
    {
        string detail;

        if (downloadingCount > 0)
        {
            detail = (downloadingCount == 1 ? "Downloading" : downloadingCount + " downloading")
                + " · " + averageLeftPercent.ToString("0", CultureInfo.CurrentCulture) + "% left";
        }
        else if (pausedCount > 0)
        {
            detail = pausedCount + (pausedCount == 1 ? " paused download" : " paused downloads");
        }
        else if (watchedSearchCount > 0)
        {
            detail = watchedSearchCount + (watchedSearchCount == 1 ? " watched search" : " watched searches");
        }
        else
        {
            return AppName;
        }

        return AppName + " · " + detail;
    }
}
