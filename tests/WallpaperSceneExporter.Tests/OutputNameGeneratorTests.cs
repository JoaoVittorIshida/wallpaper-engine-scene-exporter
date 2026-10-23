using WallpaperSceneExporter.Helpers;

namespace WallpaperSceneExporter.Tests;

public sealed class OutputNameGeneratorTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "wse-tests-" + Guid.NewGuid().ToString("N"));

    public OutputNameGeneratorTests() => Directory.CreateDirectory(_directory);

    [Fact]
    public void AddsSuffixInsteadOfOverwriting()
    {
        var now = new DateTimeOffset(2026, 8, 16, 22, 30, 45, TimeSpan.Zero);
        File.WriteAllText(Path.Combine(_directory, "scene_2026-08-16_22-30-45.gif"), "existing");
        string path = OutputNameGenerator.CreateUniquePath(_directory, ".gif", now);
        Assert.EndsWith("scene_2026-08-16_22-30-45_2.gif", path);
    }

    public void Dispose() => Directory.Delete(_directory, true);
}
