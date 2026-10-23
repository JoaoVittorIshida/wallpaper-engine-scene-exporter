using WallpaperSceneExporter.Models;

namespace WallpaperSceneExporter.Services;

// External boundaries of the pipeline: regression tests use deterministic implementations
// without starting Wallpaper Engine or acquiring a GPU capture session.
public interface ISceneConversionService
{
    Task<ConversionResult> ProcessSceneAsync(string scenePath, IProgress<PipelineProgress> progress, CancellationToken cancellationToken);
}

public interface IWallpaperEngineService
{
    Task<string> EnsureAvailableAsync(AppConfig config, CancellationToken cancellationToken);
    Task OpenSceneAsync(string executable, string scenePath, string windowName, AppConfig config, CancellationToken cancellationToken);
    Task<nint> WaitForWindowAsync(string windowName, int expectedWidth, int expectedHeight, CancellationToken cancellationToken);
    Task CloseWindowAsync(string executable, string windowName, CancellationToken cancellationToken = default);
}

public interface IWindowCaptureService
{
    Task CaptureAsync(nint hwnd, AppConfig config, string outputMp4, IProgress<double>? progress, CancellationToken cancellationToken);
}

public interface IFfmpegService
{
    Task<string> ValidateAsync(AppConfig config, CancellationToken cancellationToken = default);
    Task GenerateGifAsync(AppConfig config, string mp4Path, string palettePath, string gifPath, CancellationToken cancellationToken);
}

public interface ILoggingService
{
    Task InfoAsync(string message, CancellationToken cancellationToken = default);
    Task ErrorAsync(string message, CancellationToken cancellationToken = default);
}
