namespace WallpaperSceneExporter.Models;

public sealed record SceneWorkItem(
    string OriginalPath,
    string ProcessingPath,
    string RendererProjectPath,
    string WorkDirectory);
