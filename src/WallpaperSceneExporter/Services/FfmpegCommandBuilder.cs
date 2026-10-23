using WallpaperSceneExporter.Models;

namespace WallpaperSceneExporter.Services;

public static class FfmpegCommandBuilder
{
    public static IReadOnlyList<string> BuildRawVideoEncodeArguments(
        AppConfig config,
        string outputPath,
        int sourceWidth,
        int sourceHeight)
    {
        var arguments = new List<string>
        {
        "-hide_banner", "-loglevel", "warning", "-y",
        "-f", "rawvideo", "-pixel_format", "bgra",
        "-video_size", $"{sourceWidth}x{sourceHeight}",
        "-framerate", config.CaptureFps.ToString(System.Globalization.CultureInfo.InvariantCulture),
        "-i", "pipe:0", "-an"
        };

        if (sourceWidth != config.Width || sourceHeight != config.Height)
        {
            arguments.Add("-vf");
            arguments.Add($"scale={config.Width}:{config.Height}:flags=lanczos");
        }

        arguments.AddRange(["-c:v", "libx264", "-preset", "medium",
            "-crf", "10", "-pix_fmt", "yuv420p", "-movflags", "+faststart", outputPath]);
        return arguments;
    }

    public static IReadOnlyList<string> BuildPaletteArguments(AppConfig config, string mp4Path, string palettePath)
    {
        string filter = $"fps={config.EffectiveGifFps},scale={config.Width}:{config.Height}:flags=lanczos,palettegen=max_colors={config.EffectiveGifColors}:stats_mode=full:reserve_transparent=0";
        return ["-hide_banner", "-loglevel", "warning", "-y", "-i", mp4Path, "-vf", filter, palettePath];
    }

    public static IReadOnlyList<string> BuildGifArguments(AppConfig config, string mp4Path, string palettePath, string gifPath)
    {
        string filter = $"[0:v]fps={config.EffectiveGifFps},scale={config.Width}:{config.Height}:flags=lanczos[v];[v][1:v]paletteuse=dither={config.EffectiveGifDithering}:diff_mode=rectangle";
        return ["-hide_banner", "-loglevel", "warning", "-y", "-i", mp4Path, "-i", palettePath, "-lavfi", filter, "-loop", "0", gifPath];
    }
}
