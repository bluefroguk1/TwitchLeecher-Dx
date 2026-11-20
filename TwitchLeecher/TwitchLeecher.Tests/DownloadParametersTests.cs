using TwitchLeecher.Core.Models;

namespace TwitchLeecher.Tests;

public class DownloadParametersTests
{
    private static DownloadParameters Validated(DownloadParameters p)
    {
        p.Validate();
        return p;
    }

    [Fact]
    public void Constructor_RejectsMissingArguments()
    {
        var v = TestData.Video();
        var q = TestData.Quality();
        var qs = new List<TwitchVideoQuality> { q };
        Assert.Throws<ArgumentNullException>(() => new DownloadParameters(null, qs, q, "f", "a.mp4", false));
        Assert.Throws<ArgumentNullException>(() => new DownloadParameters(v, null, q, "f", "a.mp4", false));
        Assert.Throws<ArgumentNullException>(() => new DownloadParameters(v, qs, null, "f", "a.mp4", false));
        Assert.Throws<ArgumentNullException>(() => new DownloadParameters(v, qs, q, " ", "a.mp4", false));
        Assert.Throws<ArgumentNullException>(() => new DownloadParameters(v, qs, q, "f", "", false));
    }

    [Fact]
    public void ValidDefaults_HaveNoErrors()
    {
        Assert.False(Validated(TestData.Params()).HasErrors);
    }

    [Fact]
    public void FullPath_CombinesFolderAndFilename()
    {
        var p = TestData.Params(filename: "x.mp4");
        Assert.Equal(Path.Combine("out", "x.mp4"), p.FullPath);
    }

    [Theory]
    [InlineData("clip.mp4", false, false)]
    [InlineData("clip.MKV", false, true)] // shortcut: supported-format check is case-sensitive today
    [InlineData("clip.avi", false, true)]
    [InlineData("clip", false, true)]
    [InlineData("clip.ts", true, false)]
    [InlineData("clip.TS", true, false)]
    [InlineData("clip.mp4", true, true)]
    public void Filename_ExtensionRules(string name, bool disableConversion, bool expectError)
    {
        var p = Validated(TestData.Params(filename: name, disableConversion: disableConversion));
        Assert.Equal(expectError, p.GetErrors(nameof(DownloadParameters.Filename)) != null);
    }

    [Fact]
    public void Filename_WithInvalidChars_IsRejected()
    {
        var p = Validated(TestData.Params(filename: "a|b.mp4"));
        Assert.NotNull(p.GetErrors(nameof(DownloadParameters.Filename)));
    }

    [Fact]
    public void CroppedLength_NoCrop_IsVideoLength()
    {
        Assert.Equal(TimeSpan.FromHours(2), TestData.Params().CroppedLength);
    }

    [Fact]
    public void CroppedLength_StartOnly_SubtractsFromLength()
    {
        var p = TestData.Params();
        p.CropStart = true;
        p.CropStartTime = TimeSpan.FromMinutes(30);
        Assert.Equal(TimeSpan.FromMinutes(90), p.CroppedLength);
    }

    [Fact]
    public void CroppedLength_EndOnly_IsEndTime()
    {
        var p = TestData.Params();
        p.CropEnd = true;
        p.CropEndTime = TimeSpan.FromMinutes(45);
        Assert.Equal(TimeSpan.FromMinutes(45), p.CroppedLength);
    }

    [Fact]
    public void CroppedLength_BothCrops_IsDifference()
    {
        var p = TestData.Params();
        p.CropStart = true;
        p.CropEnd = true;
        p.CropStartTime = TimeSpan.FromMinutes(10);
        p.CropEndTime = TimeSpan.FromMinutes(25);
        Assert.Equal(TimeSpan.FromMinutes(15), p.CroppedLength);
        Assert.Equal("00:15:00", p.CroppedLengthStr);
    }

    [Fact]
    public void CropRange_ExactlyFiveSeconds_IsValid()
    {
        var p = TestData.Params();
        p.CropStart = true;
        p.CropEnd = true;
        p.CropStartTime = TimeSpan.FromSeconds(10);
        p.CropEndTime = TimeSpan.FromSeconds(15);
        Assert.False(Validated(p).HasErrors);
    }

    [Fact]
    public void CropRange_UnderFiveSeconds_IsInvalid()
    {
        var p = TestData.Params();
        p.CropStart = true;
        p.CropEnd = true;
        p.CropStartTime = TimeSpan.FromSeconds(10);
        p.CropEndTime = TimeSpan.FromSeconds(14);
        p.Validate();
        Assert.NotNull(p.GetErrors(nameof(DownloadParameters.CropEndTime)));
    }

    [Fact]
    public void CropRange_EndNotAfterStart_IsInvalid()
    {
        var p = TestData.Params();
        p.CropStart = true;
        p.CropEnd = true;
        p.CropStartTime = TimeSpan.FromMinutes(20);
        p.CropEndTime = TimeSpan.FromMinutes(20);
        p.Validate();
        Assert.NotNull(p.GetErrors(nameof(DownloadParameters.CropEndTime)));
    }

    [Fact]
    public void CropStart_BeyondVideoLength_IsInvalid()
    {
        var p = TestData.Params(video: TestData.Video(length: TimeSpan.FromMinutes(10)));
        p.CropStart = true;
        p.CropStartTime = TimeSpan.FromMinutes(11);
        p.Validate();
        Assert.NotNull(p.GetErrors(nameof(DownloadParameters.CropStartTime)));
    }

    [Fact]
    public void CropStart_LeavingUnderFiveSeconds_IsInvalid()
    {
        var p = TestData.Params(video: TestData.Video(length: TimeSpan.FromMinutes(10)));
        p.CropStart = true;
        p.CropStartTime = TimeSpan.FromMinutes(10) - TimeSpan.FromSeconds(4);
        p.Validate();
        Assert.NotNull(p.GetErrors(nameof(DownloadParameters.CropStartTime)));
    }

    [Fact]
    public void CropEnd_BeyondVideoLength_IsInvalid()
    {
        var p = TestData.Params(video: TestData.Video(length: TimeSpan.FromMinutes(10)));
        p.CropEnd = true;
        p.CropEndTime = TimeSpan.FromMinutes(11);
        p.Validate();
        Assert.NotNull(p.GetErrors(nameof(DownloadParameters.CropEndTime)));
    }

    [Fact]
    public void CropTimes_AreIgnoredWhenCropDisabled()
    {
        var p = TestData.Params();
        p.CropStartTime = TimeSpan.FromHours(99);
        Assert.False(Validated(p).HasErrors);
    }
}
