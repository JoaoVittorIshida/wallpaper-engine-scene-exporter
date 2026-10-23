namespace WallpaperSceneExporter.Helpers;

public static class OutputNameGenerator
{
    public static string CreateUniquePath(string directory, string extension, DateTimeOffset? now = null)
    {
        Directory.CreateDirectory(directory);
        string stem = $"scene_{(now ?? DateTimeOffset.Now):yyyy-MM-dd_HH-mm-ss}";
        string candidate = Path.Combine(directory, stem + extension);
        for (int suffix = 2; File.Exists(candidate); suffix++)
            candidate = Path.Combine(directory, $"{stem}_{suffix}{extension}");
        return candidate;
    }
}
