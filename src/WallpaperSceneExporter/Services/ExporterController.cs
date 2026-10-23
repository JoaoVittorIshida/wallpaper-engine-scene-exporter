using System.Threading.Channels;
using WallpaperSceneExporter.Helpers;
using WallpaperSceneExporter.Models;

namespace WallpaperSceneExporter.Services;

public sealed class ExporterController : IAsyncDisposable, IDisposable
{
    private readonly AppPaths _paths;
    private readonly InputWatcherService _watcher;
    private readonly ISceneConversionService _conversion;
    private readonly ConfigService _config;
    private readonly Channel<bool> _requests = Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
    {
        FullMode = BoundedChannelFullMode.DropWrite,
        SingleReader = true,
        SingleWriter = false
    });
    private readonly CancellationTokenSource _lifetime = new();
    private CancellationTokenSource? _currentConversion;
    private Task? _worker;
    private volatile bool _started;
    private volatile bool _busy;
    private readonly object _disposeLock = new();
    private Task? _disposeTask;

    public ExporterController(AppPaths paths, InputWatcherService watcher, ISceneConversionService conversion, ConfigService config)
    {
        _paths = paths;
        _watcher = watcher;
        _conversion = conversion;
        _config = config;
        _watcher.SceneChanged += OnSceneChanged;
        _watcher.WatcherError += (_, ex) => ControllerError?.Invoke(this, ex.Message);
    }

    public event EventHandler<PipelineProgress>? ProgressChanged;
    public event EventHandler<ConversionResult>? ConversionFinished;
    public event EventHandler<bool>? InputAvailabilityChanged;
    public event EventHandler<bool>? BusyChanged;
    public event EventHandler<string>? ControllerError;

    public bool IsBusy => _busy;
    public bool HasInput => File.Exists(_paths.InputSceneFile);

    public void Initialize()
    {
        if (_started) return;
        _started = true;
        _watcher.Start();
        _worker = Task.Run(() => WorkerAsync(_lifetime.Token));
        InputAvailabilityChanged?.Invoke(this, HasInput);
        if (_config.Current.AutoProcess && HasInput) RequestStart();
    }

    public bool RequestStart()
    {
        if (!_started || _lifetime.IsCancellationRequested || _busy || !HasInput) return false;
        return _requests.Writer.TryWrite(true);
    }

    public void Retry() => RequestStart();
    public void Cancel()
    {
        try { Volatile.Read(ref _currentConversion)?.Cancel(); }
        catch (ObjectDisposedException) { }
    }

    private void OnSceneChanged(object? sender, EventArgs e)
    {
        bool available = HasInput;
        InputAvailabilityChanged?.Invoke(this, available);
        if (available && _config.Current.AutoProcess) RequestStart();
    }

    private async Task WorkerAsync(CancellationToken cancellationToken)
    {
        var progress = new CallbackProgress(p => ProgressChanged?.Invoke(this, p));
        try
        {
            await foreach (bool _ in _requests.Reader.ReadAllAsync(cancellationToken))
            {
                if (!HasInput || _busy) continue;
                SetBusy(true);
                using var conversionSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                Volatile.Write(ref _currentConversion, conversionSource);
                bool processNext = false;
                try
                {
                    ConversionResult result = await _conversion.ProcessSceneAsync(_paths.InputSceneFile, progress, conversionSource.Token);
                    processNext = result.Success || result.HasPendingInput;
                    ConversionFinished?.Invoke(this, result);
                    if (result.Success)
                    {
                        await Task.Delay(900, cancellationToken);
                        ProgressChanged?.Invoke(this, new(PipelineState.Waiting, "Pronto para a próxima Scene", 0));
                    }
                }
                catch (OperationCanceledException) when (conversionSource.IsCancellationRequested)
                {
                    ProgressChanged?.Invoke(this, new(PipelineState.Waiting, "Conversão cancelada — scene.pkg preservado", 0));
                    ConversionFinished?.Invoke(this, ConversionResult.Failed("Conversão cancelada."));
                }
                catch (Exception ex)
                {
                    ProgressChanged?.Invoke(this, new(PipelineState.Error, ex.Message, 0));
                    ConversionFinished?.Invoke(this, ConversionResult.Failed(ex.Message));
                }
                finally
                {
                    Volatile.Write(ref _currentConversion, null);
                    SetBusy(false);
                    InputAvailabilityChanged?.Invoke(this, HasInput);
                    if (processNext && _config.Current.AutoProcess && !cancellationToken.IsCancellationRequested && HasInput)
                        RequestStart();
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
    }

    private void SetBusy(bool value)
    {
        _busy = value;
        BusyChanged?.Invoke(this, value);
    }

    public ValueTask DisposeAsync()
    {
        lock (_disposeLock)
            return new ValueTask(_disposeTask ??= StopAsync());
    }

    private async Task StopAsync()
    {
        _started = false;
        _lifetime.Cancel();
        Cancel();
        _requests.Writer.TryComplete();
        _watcher.Dispose();
        if (_worker is not null)
        {
            try { await _worker.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
        }
        _lifetime.Dispose();
    }

    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();

    private sealed class CallbackProgress(Action<PipelineProgress> callback) : IProgress<PipelineProgress>
    {
        public void Report(PipelineProgress value) => callback(value);
    }
}
