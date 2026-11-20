using TwitchLeecher.Core.Models;
using TwitchLeecher.Services.Services;

namespace TwitchLeecher.Tests;

public class FilenameServiceTests
{
    private readonly FilenameService _sut = new();

    [Fact]
    public void SubstituteWildcards_ReplacesAllWildcards()
    {
        var video = TestData.Video();
        var q = TestData.Quality(res: "1280x720");

        string result = _sut.SubstituteWildcards(
            "{channel}-{id}-{title}-{res}-{year}{month}{day}-{hr24}{hr}{min}{sec}{apm}", video, q);

        Assert.Equal("SomeChannel-123456-My Title-1280x720-20230704-15030809PM", result);
    }

    [Fact]
    public void SubstituteWildcards_CropWildcards_DefaultToWholeVideo()
    {
        var video = TestData.Video(length: new TimeSpan(1, 2, 3));
        string result = _sut.SubstituteWildcards("{start}_{end}", video, TestData.Quality());
        Assert.Equal("000000_010203", result);
    }

    [Fact]
    public void SubstituteWildcards_CropWildcards_UseGivenValues()
    {
        var video = TestData.Video(length: TimeSpan.FromHours(30));
        string result = _sut.SubstituteWildcards("{start}_{end}", video, TestData.Quality(),
            new TimeSpan(0, 5, 6), new TimeSpan(26, 0, 1));
        Assert.Equal("000506_260001", result);
    }

    [Fact]
    public void SubstituteWildcards_InvalidCharsInTitle_AreReplaced()
    {
        var video = TestData.Video(title: "a/b:c*d?e\"f<g>h|i");
        string result = _sut.SubstituteWildcards("{title}.mp4", video, TestData.Quality());
        Assert.Equal("a_b_c_d_e_f_g_h_i.mp4", result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SubstituteWildcards_BlankFilename_ReturnedUnchanged(string name)
    {
        Assert.Equal(name, _sut.SubstituteWildcards(name, TestData.Video(), TestData.Quality()));
    }

    [Fact]
    public void SubstituteWildcards_NullVideo_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => _sut.SubstituteWildcards("x", null, TestData.Quality()));
    }

    [Fact]
    public void SubstituteWildcards_NoWildcards_Unchanged()
    {
        Assert.Equal("plain.mp4", _sut.SubstituteWildcards("plain.mp4", TestData.Video(), TestData.Quality()));
    }

    [Fact]
    public void SubstituteInvalidChars_ReplacesAdditionalChars()
    {
        Assert.Equal("a_b_c_d_e_f_g", _sut.SubstituteInvalidChars("a\"b<c>d|e*f?g", "_"));
    }

    [Fact]
    public void SubstituteInvalidChars_UsesGivenReplacement()
    {
        Assert.Equal("a-b", _sut.SubstituteInvalidChars("a:b", "-"));
    }

    [Fact]
    public void SubstituteInvalidChars_EmptyReplacement_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => _sut.SubstituteInvalidChars("a:b", ""));
    }

    [Theory]
    [InlineData("clip", false, "clip.mp4")]
    [InlineData("clip.mp4", false, "clip.mp4")]
    [InlineData("clip.mkv", false, "clip.mkv")]
    [InlineData("clip.ts", false, "clip.ts.mp4")] // shortcut: current behaviour, .ts is not swapped when conversion is on
    [InlineData("clip", true, "clip.ts")]
    [InlineData("clip.ts", true, "clip.ts")]
    [InlineData("clip.mp4", true, "clip.mp4.ts")] // shortcut: current behaviour, extension is appended
    public void EnsureExtension_CurrentBehaviour(string input, bool disableConversion, string expected)
    {
        Assert.Equal(expected, _sut.EnsureExtension(input, disableConversion));
    }

    [Fact]
    public void EnsureExtension_Blank_Unchanged()
    {
        Assert.Null(_sut.EnsureExtension(null, false));
        Assert.Equal("", _sut.EnsureExtension("", true));
    }
}
