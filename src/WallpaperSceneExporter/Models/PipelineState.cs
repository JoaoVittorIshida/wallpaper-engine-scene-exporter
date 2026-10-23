namespace WallpaperSceneExporter.Models;

public enum PipelineState
{
    Waiting,
    Preparing,
    Rendering,
    Recording,
    Converting,
    Completed,
    Error
}
