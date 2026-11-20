using TwitchLeecher.Core.Enums;
using TwitchLeecher.Core.Models;
using TwitchLeecher.Shared.Extensions;
using TwitchLeecher.Shared.IO;

namespace TwitchLeecher.Tests;

public class TwitchVideoQualityTests
{
    [Fact]
    public void Constructor_ParsesVerticalResolution()
    {
        var q = new TwitchVideoQuality("720p60", "720p60", "1280x720");
        Assert.Equal(720, q.VerticalResolution);
        Assert.Equal("720p60 (1280x720)", q.DisplayString);
        Assert.Equal(q.DisplayString, q.ToString());
    }

    [Fact]
    public void Constructor_NoResolution_HasZeroHeightAndPlainName()
    {
        var q = new TwitchVideoQuality("audio_only", "Audio Only");
        Assert.Equal(0, q.VerticalResolution);
        Assert.Equal("Audio Only", q.DisplayString);
        Assert.True(q.IsAudioOnly);
        Assert.False(q.IsSource);
    }

    [Fact]
    public void Constructor_FlagsSource()
    {
        Assert.True(new TwitchVideoQuality("chunked", "1080p60 (Source)", "1920x1080").IsSource);
    }

    [Theory]
    [InlineData(null, "n")]
    [InlineData("", "n")]
    [InlineData("i", null)]
    [InlineData("i", " ")]
    public void Constructor_RequiresIdAndName(string id, string name)
    {
        Assert.Throws<ArgumentNullException>(() => new TwitchVideoQuality(id, name));
    }

    [Fact]
    public void Sort_OrdersHighestResolutionFirst_AudioOnlyLast()
    {
        var list = new List<TwitchVideoQuality>
        {
            new("audio_only", "Audio Only"),
            new("480p30", "480p", "852x480"),
            new("chunked", "1080p60 (Source)", "1920x1080"),
            new("720p60", "720p60", "1280x720"),
        };
        list.Sort();
        Assert.Equal(new[] { "chunked", "720p60", "480p30", "audio_only" }, list.Select(q => q.Id));
    }

    [Fact]
    public void CompareTo_Null_ComesFirst()
    {
        Assert.Equal(-1, new TwitchVideoQuality("a", "a", "1x1").CompareTo(null));
    }

    [Fact]
    public void Equals_UsesIdCaseInsensitive()
    {
        Assert.True(new TwitchVideoQuality("Chunked", "x", "1x1").Equals(new TwitchVideoQuality("chunked", "y", "2x2")));
        Assert.False(new TwitchVideoQuality("a", "x").Equals(new TwitchVideoQuality("b", "x")));
    }
}

public class TwitchVideoDownloadTests
{
    [Fact]
    public void NewDownload_StartsQueuedWithUniqueId()
    {
        var a = new TwitchVideoDownload(TestData.Params());
        var b = new TwitchVideoDownload(TestData.Params());
        Assert.Equal(DownloadState.Queued, a.DownloadState);
        Assert.NotEqual(a.Id, b.Id);
        Assert.Equal("Queued", a.Status);
    }

    [Fact]
    public void Constructor_NullParams_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new TwitchVideoDownload(null));
    }

    [Fact]
    public void Status_UsesDescriptionUnlessDownloading()
    {
        var d = new TwitchVideoDownload(TestData.Params());
        d.SetDownloadState(DownloadState.CompletedWithWarning);
        Assert.Equal("Completed with warnings", d.Status);

        d.SetStatus("Downloading 50%");
        d.SetDownloadState(DownloadState.Downloading);
        Assert.Equal("Downloading 50%", d.Status);
        Assert.False(d.IsDone);

        d.SetDownloadState(DownloadState.Done);
        Assert.True(d.IsDone);
    }

    [Fact]
    public void Log_AppendAndReset()
    {
        var d = new TwitchVideoDownload(TestData.Params());
        d.AppendLog("one");
        d.AppendLog("two");
        Assert.Contains("one", d.Log);
        Assert.Contains("two", d.Log);
        d.ResetLog();
        Assert.True(string.IsNullOrEmpty(d.Log));
    }

    [Fact]
    public void SetProgress_StoresValue()
    {
        var d = new TwitchVideoDownload(TestData.Params());
        d.SetProgress(42.5);
        Assert.Equal(42.5, d.Progress);
    }
}

public class TimeSpanExtensionsTests
{
    [Theory]
    [InlineData(0, 0, 0, 0, "00:00:00")]
    [InlineData(0, 1, 2, 3, "01:02:03")]
    [InlineData(1, 2, 3, 4, "26:03:04")] // days folded into hours
    [InlineData(0, 100, 0, 0, "100:00:00")]
    public void ToDaylessString(int d, int h, int m, int s, string expected)
    {
        Assert.Equal(expected, new TimeSpan(d, h, m, s).ToDaylessString());
    }

    [Fact]
    public void ToShortDaylessString_HasNoSeparators()
    {
        Assert.Equal("010203", new TimeSpan(1, 2, 3).ToShortDaylessString());
    }

    [Theory]
    [InlineData("1h2m3s", 1, 2, 3)]
    [InlineData("2h", 2, 0, 0)]
    [InlineData("5m10s", 0, 5, 10)]
    [InlineData("45s", 0, 0, 45)]
    public void ParseTwitchFormat_Valid(string input, int h, int m, int s)
    {
        Assert.Equal(new TimeSpan(h, m, s), TimeSpanExtensions.ParseTwitchFormat(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    public void ParseTwitchFormat_Invalid_Throws(string input)
    {
        Assert.Throws<ArgumentException>(() => TimeSpanExtensions.ParseTwitchFormat(input));
        Assert.False(TimeSpanExtensions.TryParseTwitchFormat(input, out var r));
        Assert.Equal(TimeSpan.Zero, r);
    }
}

public class VersionExtensionsTests
{
    [Fact]
    public void Pad_AddsMissingComponents()
    {
        Assert.Equal(new Version(1, 2, 0, 0), new Version(1, 2).Pad());
        Assert.Equal(new Version(1, 2, 3, 0), new Version(1, 2, 3).Pad());
        Assert.Equal(new Version(1, 2, 3, 4), new Version(1, 2, 3, 4).Pad());
    }

    [Fact]
    public void Trim_RemovesTrailingZeroComponents()
    {
        Assert.Equal(new Version(1, 2), new Version(1, 2, 0, 0).Trim());
        Assert.Equal(new Version(1, 2, 3), new Version(1, 2, 3, 0).Trim());
        Assert.Equal(new Version(1, 2, 3, 4), new Version(1, 2, 3, 4).Trim());
    }
}

public class FileSystemTests
{
    [Theory]
    [InlineData("good name.mp4", false)]
    [InlineData("a|b", true)]
    [InlineData("a?b", true)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void FilenameContainsInvalidChars(string name, bool expected)
    {
        Assert.Equal(expected, FileSystem.FilenameContainsInvalidChars(name));
    }
}
