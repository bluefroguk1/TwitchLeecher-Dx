using TwitchLeecher.Core.Models;

namespace TwitchLeecher.Services.Interfaces
{
    // Queues videos with the default quality, folder and filename. Used by quick download,
    // "Download Selected" and channel monitoring.
    public interface IQuickDownloadService
    {
        // Network calls only, no UI: safe on a background thread. Returns null and sets problem if it can't be downloaded.
        DownloadParameters CreateDownloadParams(TwitchVideo video, out string problem);

        // Same checks as the download dialog, minus the questions: never overwrites. Returns why it was skipped, or null.
        // Changes the download queue, so call it on the UI thread.
        string TryEnqueue(DownloadParameters downloadParams);
    }
}
