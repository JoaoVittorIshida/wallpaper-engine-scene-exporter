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
    public async Task BundledMediaToolsConvertRawCaptureToValidatedMp4AndAnimatedGif()
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        CancellationToken token = timeout.Token;
        var paths = new AppPaths(Path.Combine(_directory, "media with spaces"));
        paths.EnsureDirectories();
        var config = new AppConfig { Width = 48, Height = 48, CaptureFps = 12, DurationSeconds = 2, GifFps = 6 };
        string ffmpeg = Path.Combine(AppContext.BaseDirectory, "ffmpeg", "ffmpeg.exe");
        string ffprobe = Path.Combine(AppContext.BaseDirectory, "ffmpeg", "ffprobe.exe");
        string mp4 = Path.Combine(paths.OutputDirectory, "capture.mp4");
        string palette = Path.Combine(paths.OutputDirectory, "palette.png");
        string gif = Path.Combine(paths.OutputDirectory, "animation.gif");
        var runner = new ProcessRunner();
        using (var process = runner.StartWithInput(ffmpeg,
            FfmpegCommandBuilder.BuildRawVideoEncodeArguments(config, mp4, 32, 24)))
        {
            Task<string> errors = process.StandardError.ReadToEndAsync(token);
            try
            {
                byte[] frame = new byte[32 * 24 * 4];
                for (int index = 0; index < 24; index++)
                {
                    for (int pixel = 0; pixel < frame.Length; pixel += 4)
                    {
                        frame[pixel] = (byte)(index * 10);
                        frame[pixel + 1] = (byte)(pixel % 256);
                        frame[pixel + 2] = (byte)(255 - index * 10);
                        frame[pixel + 3] = 255;
                    }
                    await process.StandardInput.BaseStream.WriteAsync(frame, token);
                }
                process.StandardInput.Close();
                await process.WaitForExitAsync(token);
                Assert.True(process.ExitCode == 0, await errors);
            }
            finally { ProcessRunner.TryKill(process); }
        }

        await runner.RunAsync(ffmpeg, FfmpegCommandBuilder.BuildPaletteArguments(config, mp4, palette), cancellationToken: token);
        await runner.RunAsync(ffmpeg, FfmpegCommandBuilder.BuildGifArguments(config, mp4, palette, gif), cancellationToken: token);
        foreach (var (file, frames, codec) in new[] { (mp4, 24, "h264"), (gif, 12, "gif") })
        {
            string json = await runner.CaptureStandardOutputAsync(ffprobe,
                ["-v", "error", "-select_streams", "v:0", "-count_frames", "-show_entries",
                 "stream=codec_name,width,height,nb_read_frames", "-of", "json", file], TimeSpan.FromSeconds(10), token);
            using var document = System.Text.Json.JsonDocument.Parse(json);
            var stream = document.RootElement.GetProperty("streams")[0];
            Assert.Equal(codec, stream.GetProperty("codec_name").GetString());
            Assert.Equal(48, stream.GetProperty("width").GetInt32());
            Assert.Equal(48, stream.GetProperty("height").GetInt32());
            Assert.Equal(frames.ToString(System.Globalization.CultureInfo.InvariantCulture), stream.GetProperty("nb_read_frames").GetString());
        }
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
