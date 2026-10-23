namespace WallpaperSceneExporter.Helpers;

public sealed class AppPaths
{
    public AppPaths(string baseDirectory)
    {
        BaseDirectory = Path.GetFullPath(baseDirectory);
        InputDirectory = Path.Combine(BaseDirectory, "input");
        OutputDirectory = Path.Combine(BaseDirectory, "output");
        TempDirectory = Path.Combine(BaseDirectory, "temp");
        LogsDirectory = Path.Combine(BaseDirectory, "logs");
        ConfigFile = Path.Combine(BaseDirectory, "config.json");
        InputSceneFile = Path.Combine(InputDirectory, "scene.pkg");
        LatestLogFile = Path.Combine(LogsDirectory, "latest.log");
    }

    public string BaseDirectory { get; }
    public string InputDirectory { get; }
    public string OutputDirectory { get; }
    public string TempDirectory { get; }
    public string LogsDirectory { get; }
    public string ConfigFile { get; }
    public string InputSceneFile { get; }
    public string LatestLogFile { get; }

    public void EnsureDirectories()
    {
        Directory.CreateDirectory(InputDirectory);
        Directory.CreateDirectory(OutputDirectory);
        Directory.CreateDirectory(TempDirectory);
        Directory.CreateDirectory(LogsDirectory);
    }
}
