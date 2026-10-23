using WallpaperSceneExporter.Helpers;
using WallpaperSceneExporter.Models;
using WallpaperSceneExporter.Services;

namespace WallpaperSceneExporter.Tests;

public sealed class ExporterControllerTests : IDisposable
{
    private readonly AppPaths _paths = new(Path.Combine(Path.GetTempPath(), "wse-controller-" + Guid.NewGuid().ToString("N")));

    public ExporterControllerTests() => _paths.EnsureDirectories();

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task NewSceneArrivingWhileBusyIsProcessedAfterTheCurrentWork(bool firstSucceeds)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        await File.WriteAllTextAsync(_paths.InputSceneFile, "first", token);
        var firstClaimed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finishFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int calls = 0;
        var converter = new DelegateConverter(async (path, cancellation) =>
        {
            int number = Interlocked.Increment(ref calls);
            if (number == 1)
            {
                File.Move(path, Path.Combine(_paths.TempDirectory, "claimed.pkg"));
                firstClaimed.SetResult();
                await finishFirst.Task.WaitAsync(cancellation);
                return firstSucceeds ? ConversionResult.Ok("first.gif")
                    : ConversionResult.Failed("first failed") with { HasPendingInput = true };
            }
            Assert.Equal("second", await File.ReadAllTextAsync(path, cancellation));
            File.Delete(path);
            secondStarted.SetResult();
            return ConversionResult.Ok("second.gif");
        });
        var config = new ConfigService(_paths);
        config.Current.AutoProcess = true;
        await using var controller = new ExporterController(_paths, new InputWatcherService(_paths), converter, config);
        controller.Initialize();
        await firstClaimed.Task.WaitAsync(TimeSpan.FromSeconds(5), token);
        await File.WriteAllTextAsync(_paths.InputSceneFile, "second", token);
        Assert.True(controller.IsBusy);
        finishFirst.SetResult();

        await secondStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), token);

        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task FailureDoesNotAutomaticallyRepeatTheRestoredScene()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        await File.WriteAllTextAsync(_paths.InputSceneFile, "original", token);
        var idle = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int calls = 0;
        var converter = new DelegateConverter((_, _) =>
        {
            Interlocked.Increment(ref calls);
            return Task.FromResult(ConversionResult.Failed("preserved"));
        });
        var config = new ConfigService(_paths);
        config.Current.AutoProcess = true;
        await using var controller = new ExporterController(_paths, new InputWatcherService(_paths), converter, config);
        controller.BusyChanged += (_, busy) => { if (!busy) idle.TrySetResult(); };
        controller.Initialize();
        await idle.Task.WaitAsync(TimeSpan.FromSeconds(5), token);
        // Give an incorrectly queued retry a chance to run; no filesystem event is generated here.
        await Task.Delay(150, token);

        Assert.Equal(1, calls);
        Assert.True(File.Exists(_paths.InputSceneFile));
    }

    [Fact]
    public async Task ConcurrentDisposalWaitsForCancelledWorkToFinishRecovery()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        await File.WriteAllTextAsync(_paths.InputSceneFile, "original", token);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var recovering = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowRecovery = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        bool recovered = false;
        var converter = new DelegateConverter(async (_, cancellation) =>
        {
            started.SetResult();
            try { await Task.Delay(Timeout.InfiniteTimeSpan, cancellation); }
            finally
            {
                recovering.SetResult();
                await allowRecovery.Task.WaitAsync(token);
                recovered = true;
            }
            return ConversionResult.Failed("cancelled");
        });
        var controller = new ExporterController(_paths, new InputWatcherService(_paths), converter, new ConfigService(_paths));
        controller.Initialize();
        Assert.True(controller.RequestStart());
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5), token);
        Task first = controller.DisposeAsync().AsTask();
        Task second = controller.DisposeAsync().AsTask();
        try
        {
            await recovering.Task.WaitAsync(TimeSpan.FromSeconds(5), token);
            Assert.False(first.IsCompleted);
            Assert.False(second.IsCompleted);
        }
        finally { allowRecovery.TrySetResult(); }

        await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(5), token);

        Assert.True(recovered);
        Assert.False(controller.IsBusy);
        Assert.False(controller.RequestStart());
    }

    public void Dispose() => Directory.Delete(_paths.BaseDirectory, true);

    private sealed class DelegateConverter(Func<string, CancellationToken, Task<ConversionResult>> convert) : ISceneConversionService
    {
        public Task<ConversionResult> ProcessSceneAsync(string scenePath, IProgress<PipelineProgress> progress, CancellationToken cancellationToken) =>
            convert(scenePath, cancellationToken);
    }
}
