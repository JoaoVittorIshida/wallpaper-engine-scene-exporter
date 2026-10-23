using WallpaperSceneExporter.Helpers;

namespace WallpaperSceneExporter.Services;

public sealed class LoggingService : ILoggingService, IDisposable
{
    private readonly StreamWriter _writer;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public LoggingService(AppPaths paths)
    {
        paths.EnsureDirectories();
        _writer = new StreamWriter(new FileStream(paths.LatestLogFile, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
        {
            AutoFlush = true
        };
    }

    public event EventHandler<string>? LineWritten;

    public Task InfoAsync(string message, CancellationToken cancellationToken = default) => WriteAsync("INFO", message, cancellationToken);
    public Task ErrorAsync(string message, CancellationToken cancellationToken = default) => WriteAsync("ERROR", message, cancellationToken);

    private async Task WriteAsync(string level, string message, CancellationToken cancellationToken)
    {
        string line = $"[{DateTime.Now:HH:mm:ss}] [{level}] {message}";
        await _gate.WaitAsync(cancellationToken);
        try { await _writer.WriteLineAsync(line); }
        finally { _gate.Release(); }
        LineWritten?.Invoke(this, line);
    }

    public void Dispose()
    {
        _writer.Dispose();
        _gate.Dispose();
    }
}
