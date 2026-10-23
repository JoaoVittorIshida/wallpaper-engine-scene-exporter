namespace WallpaperSceneExporter.Models;

public sealed record ConversionResult(bool Success, string? OutputPath, string? ErrorMessage)
{
    public bool HasPendingInput { get; init; }
    public static ConversionResult Ok(string outputPath) => new(true, outputPath, null);
    public static ConversionResult Failed(string error) => new(false, null, error);
}
