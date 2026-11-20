using TwitchLeecher.Core.Enums;
using TwitchLeecher.Core.Models;

namespace TwitchLeecher.Tests;

public class SearchParametersTests
{
    private static readonly string NL = Environment.NewLine;

    private static SearchParameters Channel(string channel = "chan") => new(SearchType.Channel)
    {
        Channel = channel,
        LoadLimitType = LoadLimitType.LastVods,
        LoadLastVods = 10,
    };

    private static bool HasError(SearchParameters p, string prop)
    {
        p.Validate();
        return p.GetErrors(prop) != null;
    }

    [Fact]
    public void Channel_Valid_HasNoErrors()
    {
        var p = Channel();
        p.Validate();
        Assert.False(p.HasErrors);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Channel_Required(string name)
    {
        Assert.True(HasError(Channel(name), nameof(SearchParameters.Channel)));
    }

    [Fact]
    public void Channel_NotRequiredForUrlSearch()
    {
        var p = new SearchParameters(SearchType.Urls) { Urls = "https://www.twitch.tv/videos/123" };
        Assert.False(HasError(p, nameof(SearchParameters.Channel)));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    [InlineData(999, false)]
    [InlineData(1000, true)]
    public void LastVods_Range(int n, bool expectError)
    {
        var p = Channel();
        p.LoadLastVods = n;
        Assert.Equal(expectError, HasError(p, nameof(SearchParameters.LoadLastVods)));
    }

    private static SearchParameters Timespan(DateTime? from, DateTime? to) => new(SearchType.Channel)
    {
        Channel = "c",
        LoadLimitType = LoadLimitType.Timespan,
        LoadFrom = from,
        LoadTo = to,
    };

    [Fact]
    public void Timespan_ValidRange_HasNoErrors()
    {
        var p = Timespan(new DateTime(2022, 1, 1), new DateTime(2022, 2, 1));
        p.Validate();
        Assert.False(p.HasErrors);
    }

    [Fact]
    public void Timespan_DatesRequired()
    {
        var p = Timespan(null, null);
        p.Validate();
        Assert.NotNull(p.GetErrors(nameof(SearchParameters.LoadFrom)));
        Assert.NotNull(p.GetErrors(nameof(SearchParameters.LoadTo)));
    }

    [Fact]
    public void Timespan_ToBeforeFrom_IsInvalid()
    {
        Assert.True(HasError(Timespan(new DateTime(2022, 3, 1), new DateTime(2022, 2, 1)), nameof(SearchParameters.LoadTo)));
    }

    [Fact]
    public void Timespan_SameDay_IsValid()
    {
        Assert.False(HasError(Timespan(new DateTime(2022, 3, 1), new DateTime(2022, 3, 1, 23, 0, 0)), nameof(SearchParameters.LoadTo)));
    }

    [Fact]
    public void Timespan_FromBefore2010_IsInvalid()
    {
        Assert.True(HasError(Timespan(new DateTime(2009, 12, 31), new DateTime(2022, 1, 1)), nameof(SearchParameters.LoadFrom)));
    }

    [Fact]
    public void Timespan_FutureDates_AreInvalid()
    {
        var future = DateTime.Now.Date.AddDays(2);
        var p = Timespan(future, future);
        p.Validate();
        Assert.NotNull(p.GetErrors(nameof(SearchParameters.LoadFrom)));
        Assert.NotNull(p.GetErrors(nameof(SearchParameters.LoadTo)));
    }

    [Theory]
    [InlineData("https://www.twitch.tv/videos/123456", false)]
    [InlineData("https://www.twitch.tv/video/123456", false)]
    [InlineData("https://www.twitch.tv/videos/123456?t=1h2m3s", false)]
    [InlineData("https://www.twitch.tv/chan/video/42", false)]
    [InlineData("https://www.twitch.tv/videos/0", true)]
    [InlineData("https://www.twitch.tv/videos/abc", true)]
    [InlineData("https://www.twitch.tv/videos/", true)]
    [InlineData("https://www.twitch.tv/somechannel", true)]
    [InlineData("not a url", true)]
    [InlineData("", true)]
    [InlineData("   ", true)]
    public void Urls_Parsing(string url, bool expectError)
    {
        var p = new SearchParameters(SearchType.Urls) { Urls = url };
        Assert.Equal(expectError, HasError(p, nameof(SearchParameters.Urls)));
    }

    [Fact]
    public void Urls_MultipleLines_OneBadInvalidatesAll()
    {
        var good = "https://www.twitch.tv/videos/1";
        Assert.False(HasError(new SearchParameters(SearchType.Urls) { Urls = good + NL + "https://www.twitch.tv/videos/2" }, nameof(SearchParameters.Urls)));
        Assert.True(HasError(new SearchParameters(SearchType.Urls) { Urls = good + NL + "https://www.twitch.tv/nope" }, nameof(SearchParameters.Urls)));
    }

    [Theory]
    [InlineData("123456", false)]
    [InlineData("1" + "\r\n" + "2", false)]
    [InlineData("0", true)]
    [InlineData("-5", true)]
    [InlineData("12ab", true)]
    [InlineData("", true)]
    [InlineData(null, true)]
    public void Ids_Parsing(string ids, bool expectError)
    {
        if (ids != null) ids = ids.Replace("\r\n", NL);
        var p = new SearchParameters(SearchType.Ids) { Ids = ids };
        Assert.Equal(expectError, HasError(p, nameof(SearchParameters.Ids)));
    }

    [Fact]
    public void Clone_CopiesAllFields()
    {
        var p = Channel("abc");
        p.VideoType = VideoType.Highlight;
        p.Urls = "u";
        p.Ids = "1";
        p.LoadFrom = new DateTime(2020, 1, 1);
        p.LoadTo = new DateTime(2020, 2, 2);
        var c = p.Clone();
        Assert.NotSame(p, c);
        Assert.Equal(p.SearchType, c.SearchType);
        Assert.Equal("abc", c.Channel);
        Assert.Equal(VideoType.Highlight, c.VideoType);
        Assert.Equal("u", c.Urls);
        Assert.Equal("1", c.Ids);
        Assert.Equal(p.LoadFrom, c.LoadFrom);
        Assert.Equal(p.LoadTo, c.LoadTo);
        Assert.Equal(10, c.LoadLastVods);
    }
}
