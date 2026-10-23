using WallpaperSceneExporter.Helpers;
using WallpaperSceneExporter.Models;
using WallpaperSceneExporter.Services;

namespace WallpaperSceneExporter.Tests;

public sealed class ConversionServiceTests : IDisposable
{
    private readonly AppPaths _paths = new(Path.Combine(Path.GetTempPath(), "wse-pipeline-" + Guid.NewGuid().ToString("N")));
    private readonly FakeRenderer _renderer = new();
    private readonly FakeCapture _capture = new();
    private readonly FakeEncoder _encoder = new();
    private readonly FakeLog _log = new();
    private readonly ConfigService _config;
    private readonly SceneWorkItemService _workItems;
    private readonly ConversionService _service;

    public ConversionServiceTests()
    {
        _paths.EnsureDirectories();
        _config = new ConfigService(_paths);
        _config.Current.WarmupSeconds = 0;
        _config.Current.Width = 16;
        _config.Current.Height = 16;
        _config.Current.DurationSeconds = 1;
        _config.Current.CaptureFps = 2;
        _workItems = new SceneWorkItemService(_paths);
        var validation = new OutputValidationService();
        _service = new ConversionService(_paths, _config, new FileStabilityService(), _renderer, _capture,
            _encoder, validation, new SceneFinalizer(validation), _workItems, _log);
    }

    [Fact]
    public async Task LogAndNotificationFailuresAfterCommitNeverDeleteTheResult()
    {
        await AddInputAsync();
        _log.OnInfo = message =>
        {
            if (message is "scene.pkg removido" or "Conversão concluída") throw new IOException("log unavailable");
        };
        var progress = new CallbackProgress(p =>
        {
            if (p.State == PipelineState.Completed) throw new InvalidOperationException("subscriber failed");
        });

        ConversionResult result = await RunAsync(progress);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.False(File.Exists(_paths.InputSceneFile));
        await new OutputValidationService().ValidateGifAsync(result.OutputPath!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CancellationAfterCommitReturnsTheValidatedResult()
    {
        await AddInputAsync();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        _log.OnInfo = message => { if (message == "scene.pkg removido") cancellation.Cancel(); };

        ConversionResult result = await _service.ProcessSceneAsync(_paths.InputSceneFile, new CallbackProgress(), cancellation.Token);

        Assert.True(result.Success);
        Assert.True(File.Exists(result.OutputPath));
        Assert.False(File.Exists(_paths.InputSceneFile));
    }

    [Fact]
    public async Task CancelledEncodingRestoresInputAndNeverPublishesAPartialGif()
    {
        await AddInputAsync();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        _encoder.Encode = async (path, token) =>
        {
            Assert.Equal(_paths.TempDirectory, Path.GetDirectoryName(path));
            await File.WriteAllTextAsync(path, "partial GIF", token);
            cancellation.Cancel();
            token.ThrowIfCancellationRequested();
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _service.ProcessSceneAsync(_paths.InputSceneFile, new CallbackProgress(), cancellation.Token));

        Assert.Equal("original", await File.ReadAllTextAsync(_paths.InputSceneFile, TestContext.Current.CancellationToken));
        Assert.Empty(Directory.EnumerateFiles(_paths.OutputDirectory));
        Assert.Empty(Directory.EnumerateFileSystemEntries(_paths.TempDirectory));
    }

    [Fact]
    public async Task FailedRestorationKeepsThePackageAndReleasesTheConversionGate()
    {
        await AddInputAsync();
        FileStream? locked = null;
        _capture.Capture = (_, _) =>
        {
            string package = Directory.GetFiles(_paths.TempDirectory, "scene.pkg", SearchOption.AllDirectories).Single();
            locked = new FileStream(package, FileMode.Open, FileAccess.Read, FileShare.None);
            throw new IOException("capture failed");
        };
        try
        {
            ConversionResult failed = await RunAsync();
            Assert.False(failed.Success);
            Assert.Single(Directory.GetFiles(_paths.TempDirectory, "scene.pkg", SearchOption.AllDirectories));
        }
        finally { locked?.Dispose(); }

        await _workItems.RecoverInterruptedWorkAsync(TestContext.Current.CancellationToken);
        _capture.Capture = null;
        ConversionResult retried = await RunAsync().WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        Assert.True(retried.Success, retried.ErrorMessage);
    }

    [Fact]
    public async Task FailedCleanupDoesNotTurnSuccessIntoFailureOrBlockTheNextRun()
    {
        await AddInputAsync();
        FileStream? locked = null;
        _capture.Capture = async (path, token) =>
        {
            string project = Directory.GetFiles(_paths.TempDirectory, "project.json", SearchOption.AllDirectories).Single();
            locked = new FileStream(project, FileMode.Open, FileAccess.Read, FileShare.None);
            await File.WriteAllTextAsync(path, "captured", token);
        };
        try { Assert.True((await RunAsync()).Success); }
        finally { locked?.Dispose(); }

        await AddInputAsync();
        _capture.Capture = null;
        Assert.True((await RunAsync().WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken)).Success);
    }

    [Fact]
    public async Task FailureWithANewScenePreservesBothAndMarksOnlyTheNewInputAsPending()
    {
        await AddInputAsync();
        _capture.Capture = async (_, token) =>
        {
            await File.WriteAllTextAsync(_paths.InputSceneFile, "new scene", token);
            throw new IOException("capture failed");
        };

        ConversionResult failed = await RunAsync();

        Assert.False(failed.Success);
        Assert.True(failed.HasPendingInput);
        Assert.Equal("new scene", await File.ReadAllTextAsync(_paths.InputSceneFile, TestContext.Current.CancellationToken));
        string preserved = Directory.GetFiles(Path.Combine(_paths.InputDirectory, "failed")).Single();
        Assert.Equal("original", await File.ReadAllTextAsync(preserved, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RestoredFailedInputIsNotMarkedAsANewAutoProcessRequest()
    {
        await AddInputAsync();
        _capture.Capture = (_, _) => throw new IOException("capture failed");

        ConversionResult failed = await RunAsync();

        Assert.False(failed.Success);
        Assert.False(failed.HasPendingInput);
        Assert.True(File.Exists(_paths.InputSceneFile));
    }

    [Fact]
    public async Task InvalidCapturedMp4PreservesInputAndDoesNotPublishAnOutput()
    {
        await AddInputAsync();
        _config.Current.OutputFormat = "mp4";

        ConversionResult failed = await RunAsync();

        Assert.False(failed.Success);
        Assert.True(File.Exists(_paths.InputSceneFile));
        Assert.Empty(Directory.GetFiles(_paths.OutputDirectory));
    }

    [Fact]
    public async Task RealMp4IsValidatedAndPublishedBeforeInputIsDeleted()
    {
        await AddInputAsync();
        _config.Current.OutputFormat = "mp4";
        _capture.Capture = TestMedia.CreateMp4Async;

        ConversionResult result = await RunAsync();

        Assert.True(result.Success, result.ErrorMessage);
        Assert.EndsWith(".mp4", result.OutputPath);
        Assert.True(File.Exists(result.OutputPath));
        Assert.False(File.Exists(_paths.InputSceneFile));
    }

    private Task AddInputAsync() => File.WriteAllTextAsync(_paths.InputSceneFile, "original", TestContext.Current.CancellationToken);
    private Task<ConversionResult> RunAsync(IProgress<PipelineProgress>? progress = null) =>
        _service.ProcessSceneAsync(_paths.InputSceneFile, progress ?? new CallbackProgress(), TestContext.Current.CancellationToken);

    public void Dispose() => Directory.Delete(_paths.BaseDirectory, true);

    private sealed class CallbackProgress(Action<PipelineProgress>? callback = null) : IProgress<PipelineProgress>
    {
        public void Report(PipelineProgress value) => callback?.Invoke(value);
    }

    private sealed class FakeRenderer : IWallpaperEngineService
    {
        public Task<string> EnsureAvailableAsync(AppConfig config, CancellationToken cancellationToken) => Task.FromResult("renderer.exe");
        public Task OpenSceneAsync(string executable, string scenePath, string windowName, AppConfig config, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<nint> WaitForWindowAsync(string windowName, int expectedWidth, int expectedHeight, CancellationToken cancellationToken) => Task.FromResult((nint)1);
        public Task CloseWindowAsync(string executable, string windowName, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeCapture : IWindowCaptureService
    {
        public Func<string, CancellationToken, Task>? Capture { get; set; }
        public Task CaptureAsync(nint hwnd, AppConfig config, string outputMp4, IProgress<double>? progress, CancellationToken cancellationToken) =>
            Capture?.Invoke(outputMp4, cancellationToken) ?? File.WriteAllTextAsync(outputMp4, "captured", cancellationToken);
    }

    private sealed class FakeEncoder : IFfmpegService
    {
        public Func<string, CancellationToken, Task>? Encode { get; set; }
        public Task<string> ValidateAsync(AppConfig config, CancellationToken cancellationToken = default) => Task.FromResult("test encoder");
        public Task GenerateGifAsync(AppConfig config, string mp4Path, string palettePath, string gifPath, CancellationToken cancellationToken) =>
            Encode?.Invoke(gifPath, cancellationToken) ?? File.WriteAllBytesAsync(gifPath, TestMedia.Gif(), cancellationToken);
    }

    private sealed class FakeLog : ILoggingService
    {
        public Action<string>? OnInfo { get; set; }
        public Task InfoAsync(string message, CancellationToken cancellationToken = default) { OnInfo?.Invoke(message); return Task.CompletedTask; }
        public Task ErrorAsync(string message, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
