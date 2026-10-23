using WallpaperSceneExporter.Helpers;

namespace WallpaperSceneExporter.Services;

public sealed class InputWatcherService : IDisposable
{
    private readonly AppPaths _paths;
    private readonly FileSystemWatcher _watcher;

    public event EventHandler? SceneChanged;
    public event EventHandler<Exception>? WatcherError;

    public InputWatcherService(AppPaths paths)
    {
        _paths = paths;
        _watcher = new FileSystemWatcher(paths.InputDirectory, "scene.pkg")
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.Size | NotifyFilters.LastWrite,
            IncludeSubdirectories = false
        };
        _watcher.Created += OnChanged;
        _watcher.Changed += OnChanged;
        _watcher.Renamed += OnChanged;
        _watcher.Deleted += OnChanged;
        _watcher.Error += (_, e) => WatcherError?.Invoke(this, e.GetException());
    }

    public void Start()
    {
        _watcher.EnableRaisingEvents = true;
    }

    private void OnChanged(object sender, FileSystemEventArgs e) => SceneChanged?.Invoke(this, EventArgs.Empty);

    public void Dispose()
    {
        _watcher.Dispose();
    }
}
