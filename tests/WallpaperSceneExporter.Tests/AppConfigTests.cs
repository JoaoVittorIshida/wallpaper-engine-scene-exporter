using WallpaperSceneExporter.Models;

namespace WallpaperSceneExporter.Tests;

public sealed class AppConfigTests
{
    [Theory]
    [InlineData("compact", 20)]
    [InlineData("high", 30)]
    [InlineData("maximum", 50)]
    public void PresetDefinesGifFpsWhenThereIsNoOverride(string preset, int expected)
    {
        var config = new AppConfig { QualityPreset = preset, GifFps = null };
        Assert.Equal(expected, config.EffectiveGifFps);
    }

    [Fact]
    public void IndividualSettingOverridesPreset()
    {
        var config = new AppConfig { QualityPreset = "compact", GifFps = 42 };
        Assert.Equal(42, config.EffectiveGifFps);
    }

    [Fact]
    public void OddResolutionIsRejected()
    {
        var config = new AppConfig { Width = 481 };
        Assert.Throws<InvalidDataException>(config.Validate);
    }

    [Theory]
    [InlineData("gif")]
    [InlineData("GIF")]
    [InlineData("mp4")]
    [InlineData("MP4")]
    public void SupportedOutputFormatsAreAccepted(string outputFormat)
    {
        var config = new AppConfig { OutputFormat = outputFormat };
        config.Validate();
    }

    [Fact]
    public void UnknownOutputFormatIsRejected()
    {
        var config = new AppConfig { OutputFormat = "avi" };
        Assert.Throws<InvalidDataException>(config.Validate);
    }

    [Fact]
    public void Mp4DoesNotValidateUnusedGifOverrides()
    {
        var config = new AppConfig { OutputFormat = "mp4", GifFps = 999, GifColors = 999, GifDithering = "invalid" };
        config.Validate();
    }
}
