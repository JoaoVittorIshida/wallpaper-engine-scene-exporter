using WallpaperSceneExporter.Services;

namespace WallpaperSceneExporter.Tests;

public sealed class FileStabilityServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "wse-tests-" + Guid.NewGuid().ToString("N"));

    public FileStabilityServiceTests() => Directory.CreateDirectory(_directory);

    [Fact]
    public async Task WaitsUntilAFileCanBeOpenedExclusively()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string path = Path.Combine(_directory, "scene.pkg");
        await File.WriteAllBytesAsync(path, [1, 2, 3], cancellationToken);
        await using var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
        var service = new FileStabilityService();
        Task wait = service.WaitUntilReadyAsync(path, TimeSpan.FromMilliseconds(10), 2, TimeSpan.FromSeconds(2), cancellationToken);
        await Task.Delay(80, cancellationToken);
        Assert.False(wait.IsCompleted);
        await locked.DisposeAsync();
        await wait;
    }

    public void Dispose() => Directory.Delete(_directory, true);
}
