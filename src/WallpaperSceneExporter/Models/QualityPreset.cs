namespace WallpaperSceneExporter.Models;

public sealed record QualityPreset(string Name, int GifFps, int GifColors)
{
    public static readonly QualityPreset Compact = new("compact", 20, 256);
    public static readonly QualityPreset High = new("high", 30, 256);
    public static readonly QualityPreset Maximum = new("maximum", 50, 256);

    public static QualityPreset FromName(string? name) => name?.Trim().ToLowerInvariant() switch
    {
        "compact" => Compact,
        "maximum" => Maximum,
        _ => High
    };
}
