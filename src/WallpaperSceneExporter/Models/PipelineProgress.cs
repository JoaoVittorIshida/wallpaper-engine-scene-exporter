namespace WallpaperSceneExporter.Models;

public sealed record PipelineProgress(PipelineState State, string Message, double Percent);
