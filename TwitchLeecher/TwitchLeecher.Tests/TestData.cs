using TwitchLeecher.Core.Models;

namespace TwitchLeecher.Tests;

internal static class TestData
{
    // Thumbnail is null on purpose: TwitchVideo's ctor starts an async download that faults
    // immediately for a null Uri, so no network access happens.
    public static TwitchVideo Video(
        string channel = "SomeChannel",
        string title = "My Title",
        string id = "123456",
        TimeSpan? length = null,
        DateTime? recorded = null) =>
        new TwitchVideo(channel, title, id, 10, length ?? TimeSpan.FromHours(2),
            recorded ?? new DateTime(2023, 7, 4, 15, 8, 9), null, new Uri("https://www.twitch.tv/videos/" + id),
            true, false, false);

    public static TwitchVideoQuality Quality(string id = "chunked", string name = "1080p60 (Source)", string res = "1920x1080") =>
        new TwitchVideoQuality(id, name, res);

    public static DownloadParameters Params(
        TwitchVideo video = null, string filename = "video.mp4", bool disableConversion = false)
    {
        video ??= Video();
        var q = Quality();
        return new DownloadParameters(video, new List<TwitchVideoQuality> { q }, q, "out", filename, disableConversion);
    }
}
