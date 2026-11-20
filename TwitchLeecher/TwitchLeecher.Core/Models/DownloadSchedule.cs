using System;

namespace TwitchLeecher.Core.Models
{
    // Off-peak window for watched-search finds and paused downloads. Whole hours, local time; the window may
    // cross midnight (23 -> 6). Start == end means all day.
    public static class DownloadSchedule
    {
        public static bool IsOpen(Preferences prefs, DateTime now)
        {
            if (prefs == null || !prefs.ScheduleEnabled)
            {
                return true;
            }

            int start = prefs.ScheduleStartHour;
            int end = prefs.ScheduleEndHour;

            if (start == end)
            {
                return true;
            }

            int hour = now.Hour;
            return start < end ? hour >= start && hour < end : hour >= start || hour < end;
        }

        // "01:00-07:00", for status text
        public static string Describe(Preferences prefs)
        {
            return prefs.ScheduleStartHour.ToString("00") + ":00-" + prefs.ScheduleEndHour.ToString("00") + ":00";
        }
    }
}
