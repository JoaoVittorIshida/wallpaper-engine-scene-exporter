using WallpaperSceneExporter.Helpers;
using WallpaperSceneExporter.Models;
using WallpaperSceneExporter.Services;

namespace WallpaperSceneExporter.Tests;

public sealed class InfrastructureTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "wse-infra-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task InvalidConfigurationIsBackedUpAndRecovered()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var paths = new AppPaths(_directory);
        paths.EnsureDirectories();
        await File.WriteAllTextAsync(paths.ConfigFile, "{ invalid json", cancellationToken);

        AppConfig config = await new ConfigService(paths).LoadAsync(cancellationToken);

        Assert.Equal("high", config.QualityPreset);
        Assert.Single(Directory.GetFiles(_directory, "config.json.invalid-*.bak"));
        Assert.Contains("qualityPreset", await File.ReadAllTextAsync(paths.ConfigFile, cancellationToken));
    }

    [Fact]
    public async Task BundledFfmpegProvidesRequiredCapabilities()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string executable = Path.Combine(AppContext.BaseDirectory, "ffmpeg", "ffmpeg.exe");
        Assert.True(File.Exists(executable), "ffmpeg.exe deve acompanhar o build de teste.");
        var paths = new AppPaths(_directory);
        paths.EnsureDirectories();
        using var log = new LoggingService(paths);
        var service = new FfmpegService(paths, new ProcessRunner(), log);

        string version = await service.ValidateAsync(new AppConfig { FfmpegPath = executable }, cancellationToken);

        Assert.Contains("ffmpeg version", version, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExternalProcessIsKilledAfterTimeout()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string commandProcessor = Environment.GetEnvironmentVariable("ComSpec") ?? throw new InvalidOperationException("ComSpec ausente.");
        var runner = new ProcessRunner();

        await Assert.ThrowsAsync<TimeoutException>(() => runner.RunAsync(
            commandProcessor,
            ["/d", "/c", "ping", "127.0.0.1", "-n", "10"],
            timeout: TimeSpan.FromMilliseconds(100),
            cancellationToken: cancellationToken));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }
}
