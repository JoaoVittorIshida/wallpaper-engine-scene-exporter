using WallpaperSceneExporter.Models;
using WallpaperSceneExporter.Services;

namespace WallpaperSceneExporter.Tests;

public sealed class FfmpegCommandBuilderTests
{
    [Fact]
    public void RawEncoder_UsesPhysicalSourceSizeAndScalesToConfiguredOutput()
    {
        var config = new AppConfig { Width = 480, Height = 480, CaptureFps = 60 };

        string arguments = string.Join(' ', FfmpegCommandBuilder.BuildRawVideoEncodeArguments(config, "out.mp4", 720, 720));

        Assert.Contains("-video_size 720x720", arguments);
        Assert.Contains("-vf scale=480:480:flags=lanczos", arguments);
    }

    [Fact]
    public void RawEncoder_DoesNotAddScaleWhenSurfaceMatchesOutput()
    {
        var config = new AppConfig { Width = 480, Height = 480 };

        IReadOnlyList<string> arguments = FfmpegCommandBuilder.BuildRawVideoEncodeArguments(config, "out.mp4", 480, 480);

        Assert.DoesNotContain("-vf", arguments);
    }

    [Fact]
    public void GifUsesOptimizedPaletteLanczosDitheringAndInfiniteLoop()
    {
        var config = new AppConfig();
        string palette = string.Join(' ', FfmpegCommandBuilder.BuildPaletteArguments(config, "in.mp4", "palette.png"));
        string gif = string.Join(' ', FfmpegCommandBuilder.BuildGifArguments(config, "in.mp4", "palette.png", "out.gif"));

        Assert.Contains("palettegen", palette);
        Assert.Contains("lanczos", palette);
        Assert.Contains("paletteuse", gif);
        Assert.Contains("sierra2_4a", gif);
        Assert.Contains("-loop 0", gif);
    }

    [Fact]
    public void ProcessArgumentsRemainSeparateForPathsWithSpaces()
    {
        IReadOnlyList<string> args = FfmpegCommandBuilder.BuildPaletteArguments(new AppConfig(), @"C:\folder with spaces\in.mp4", "palette.png");
        Assert.Contains(@"C:\folder with spaces\in.mp4", args);
    }
}
