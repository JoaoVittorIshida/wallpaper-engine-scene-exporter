using WallpaperSceneExporter.Helpers;
using WallpaperSceneExporter.Services;
using WallpaperSceneExporter.ViewModels;

namespace WallpaperSceneExporter.Tests;

public sealed class CaptureAndSettingsTests
{
    [Theory]
    [InlineData(720, 480)]
    [InlineData(480, 720)]
    [InlineData(0, 0)]
    public void ResizedOrUnavailableSurfaceCannotBeEncodedAsARepeatedLastFrame(int width, int height)
    {
        Assert.Throws<InvalidOperationException>(() => CaptureSurfaceGuard.Validate(480, 480, width, height));
    }

    [Fact]
    public void InitialPhysicalSurfaceMayDifferFromOutputWithoutBeingConsideredAResize()
    {
        CaptureSurfaceGuard.Validate(720, 720, 720, 720);
    }

    [Fact]
    public void QualityHintTracksTheOutputFormatAndNotifiesTheSettingsView()
    {
        var settings = new SettingsViewModel(new ConfigService(new AppPaths(Path.GetTempPath())));
        var changed = new List<string?>();
        settings.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        settings.OutputFormat = "MP4";

        Assert.False(settings.IsGifOutput);
        Assert.Contains("apenas ao GIF", settings.QualityHint);
        Assert.Contains(nameof(settings.IsGifOutput), changed);
        Assert.Contains(nameof(settings.QualityHint), changed);
    }
}
