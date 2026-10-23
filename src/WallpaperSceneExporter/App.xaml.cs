using System.Windows;
using System.ComponentModel;
using System.Security.Cryptography;
using System.Text;
using WallpaperSceneExporter.Helpers;
using WallpaperSceneExporter.Services;
using WallpaperSceneExporter.ViewModels;
using WallpaperSceneExporter.Views;

namespace WallpaperSceneExporter;

public partial class App : Application
{
    private ExporterController? _controller;
    private LoggingService? _log;
    private Mutex? _singleInstance;
    private bool _closing;
    private bool _shutdownReady;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try
        {
            var paths = new AppPaths(AppContext.BaseDirectory);
            string instanceHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(paths.BaseDirectory)))[..16];
            _singleInstance = new Mutex(initiallyOwned: true, $"Local\\WallpaperSceneExporter_{instanceHash}", out bool createdNew);
            if (!createdNew)
            {
                MessageBox.Show("O Scene Exporter já está aberto.", "Scene Exporter", MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown();
                return;
            }
            paths.EnsureDirectories();
            var config = new ConfigService(paths);
            await config.LoadAsync();
            _log = new LoggingService(paths);
            await _log.InfoAsync($"Scene Exporter v{typeof(App).Assembly.GetName().Version?.ToString(3)} — {paths.BaseDirectory}");
            await _log.InfoAsync($"Saída: {config.Current.OutputFormat.ToUpperInvariant()} | {config.Current.Width}x{config.Current.Height} | {config.Current.DurationSeconds}s");
            var runner = new ProcessRunner();
            var ffmpeg = new FfmpegService(paths, runner, _log);
            var wallpaper = new WallpaperEngineService(runner, _log);
            var validation = new OutputValidationService(runner, () => ffmpeg.ResolveProbeExecutable(config.Current));
            var capture = new WindowCaptureService(ffmpeg, _log);
            var workItems = new SceneWorkItemService(paths);
            await workItems.RecoverInterruptedWorkAsync();
            var conversion = new ConversionService(paths, config, new FileStabilityService(), wallpaper, capture, ffmpeg,
                validation, new SceneFinalizer(validation), workItems, _log);
            var watcher = new InputWatcherService(paths);
            _controller = new ExporterController(paths, watcher, conversion, config);
            var dependencies = new DependencyService(ffmpeg, wallpaper);

            var viewModel = new MainViewModel(paths, config, _controller, dependencies);
            var window = new MainWindow { DataContext = viewModel };
            window.Closing += OnMainWindowClosing;
            MainWindow = window;
            window.Show();
            _controller.Initialize();
            _ = viewModel.RefreshDependenciesCommand.ExecuteAsync(null);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Scene Exporter", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private async void OnMainWindowClosing(object? sender, CancelEventArgs e)
    {
        if (_shutdownReady) return;
        e.Cancel = true;
        if (_closing) return;
        _closing = true;
        if (sender is Window window) window.IsEnabled = false;
        try
        {
            if (_controller is not null) await _controller.DisposeAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Não foi possível concluir o encerramento: {ex.Message}", "Wallpaper Engine Scene Exporter",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _shutdownReady = true;
            if (sender is Window closingWindow) closingWindow.Close();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _log?.Dispose();
        try { _singleInstance?.ReleaseMutex(); } catch { }
        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}
