namespace WallpaperSceneExporter.Models;

public sealed record DependencyStatus(
    bool FfmpegReady,
    string FfmpegMessage,
    bool WallpaperEngineReady,
    string WallpaperEngineMessage,
    bool CaptureReady,
    string CaptureMessage)
{
    public bool AllReady => FfmpegReady && WallpaperEngineReady && CaptureReady;
}
