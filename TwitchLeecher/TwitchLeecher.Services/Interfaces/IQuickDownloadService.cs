using TwitchLeecher.Core.Models;

namespace TwitchLeecher.Services.Interfaces
{
    // Queues videos with the default quality, folder and filename. Used by quick download,
    // "Download Selected" and channel monitoring.
    public interface IQuickDownloadService
    {
        // Network calls only, no UI: safe on a background thread. Returns null and sets problem if it can't be downloaded.
        // quality: null uses the default quality from Settings.
        DownloadParameters CreateDownloadParams(TwitchVideo video, out string problem, DefaultQuality quality = null);

        // Same checks as the download dialog, minus the questions: never overwrites. Returns why it was skipped, or null.
        // Changes the download queue, so call it on the UI thread.
        // startPaused: the item waits in Downloads until the user presses Resume.
        string TryEnqueue(DownloadParameters downloadParams, bool startPaused = false);

        // Where quick download would save this video with the current settings (folder + filename).
        string GetDefaultPath(TwitchVideo video, TwitchVideoQuality quality);
    }
}
