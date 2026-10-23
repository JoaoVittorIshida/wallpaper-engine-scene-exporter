using WallpaperSceneExporter.Models;
using Windows.Graphics.Capture;

namespace WallpaperSceneExporter.Services;

public sealed class DependencyService
{
    private readonly FfmpegService _ffmpeg;
    private readonly WallpaperEngineService _wallpaper;

    public DependencyService(FfmpegService ffmpeg, WallpaperEngineService wallpaper)
    {
        _ffmpeg = ffmpeg;
        _wallpaper = wallpaper;
    }

    public async Task<DependencyStatus> CheckAsync(AppConfig config, CancellationToken cancellationToken = default)
    {
        bool ffmpegReady = false;
        string ffmpegMessage;
        try
        {
            string version = await _ffmpeg.ValidateAsync(config, cancellationToken);
            ffmpegReady = true;
            ffmpegMessage = version.Replace("ffmpeg version ", string.Empty, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) { ffmpegMessage = ex.Message; }

        bool wallpaperReady = false;
        string wallpaperMessage;
        try
        {
            string executable = _wallpaper.ResolveExecutable(config);
            wallpaperReady = true;
            wallpaperMessage = Path.GetDirectoryName(executable) ?? executable;
        }
        catch (Exception ex) { wallpaperMessage = ex.Message; }

        bool captureReady = GraphicsCaptureSession.IsSupported();
        string captureMessage = captureReady
            ? "Windows Graphics Capture disponível"
            : "Windows Graphics Capture não é suportado neste computador";

        return new(ffmpegReady, ffmpegMessage, wallpaperReady, wallpaperMessage, captureReady, captureMessage);
    }
}
