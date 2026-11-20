using System.Collections.ObjectModel;
using TwitchLeecher.Core.Models;
using TwitchLeecher.Services.Interfaces;

namespace TwitchLeecher.Gui.ViewModels.Results
{
    // What is ticked in the results and how big it is expected to be.
    internal class ResultSelection
    {
        private readonly IQuickDownloadService _quickDownloadService;
        private readonly Func<ObservableCollection<TwitchVideo>> _videos;
        private readonly Action _summaryChanged;

        // estimated bytes per video id; 0 = unknown, missing = not fetched yet
        private readonly Dictionary<string, long> _estimatedBytes = new Dictionary<string, long>();

        public ResultSelection(IQuickDownloadService quickDownloadService,
            Func<ObservableCollection<TwitchVideo>> videos, Action summaryChanged)
        {
            _quickDownloadService = quickDownloadService;
            _videos = videos;
            _summaryChanged = summaryChanged;
        }

        // same filter as Download Selected, so the count matches what the button queues
        public List<TwitchVideo> Selected => _videos()?.Where(v => v.IsSelected && !v.Live).ToList() ?? new List<TwitchVideo>();

        public bool HasSelection => Selected.Count > 0;

        public string Summary
        {
            get
            {
                List<TwitchVideo> selected = Selected;
                string summary = selected.Count + " selected";
                long known = selected.Sum(v => _estimatedBytes.TryGetValue(v.Id, out long b) ? b : 0);

                if (known <= 0)
                {
                    return summary;
                }

                bool allKnown = selected.All(v => _estimatedBytes.TryGetValue(v.Id, out long b) && b > 0);
                double gb = known / 1024.0 / 1024.0 / 1024.0;
                string size = gb >= 1 ? gb.ToString("0.0") + " GB" : (known / 1024.0 / 1024.0).ToString("0") + " MB";

                return summary + " · ~" + size + (allKnown ? "" : " + unknown");
            }
        }

        // shortcut: fetches the playlist (2 API calls) for every ticked video to learn its bitrate, so "Select all"
        // on a big search makes many requests; upgrade by estimating from a fixed bitrate per default quality
        public void EstimateSize(TwitchVideo video)
        {
            if (_estimatedBytes.ContainsKey(video.Id))
            {
                return;
            }

            _estimatedBytes[video.Id] = 0;

            Task.Run(() =>
            {
                DownloadParameters p = _quickDownloadService.CreateDownloadParams(video, out _);
                long bandwidth = p?.SelectedQuality?.Bandwidth ?? 0;
                return (long)(bandwidth / 8.0 * video.Length.TotalSeconds);
            }).ContinueWith(task =>
            {
                // failures (sub-only, network) just stay unknown
                _estimatedBytes[video.Id] = task.IsCompletedSuccessfully ? task.Result : 0;
                _summaryChanged();
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }
    }
}
