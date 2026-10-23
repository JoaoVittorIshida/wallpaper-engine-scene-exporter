using WallpaperSceneExporter.Helpers;
using WallpaperSceneExporter.Models;

namespace WallpaperSceneExporter.Services;

public sealed class SceneWorkItemService
{
    private readonly AppPaths _paths;

    public SceneWorkItemService(AppPaths paths) => _paths = paths;

    public async Task RecoverInterruptedWorkAsync(CancellationToken cancellationToken = default)
    {
        foreach (string directory in Directory.EnumerateDirectories(_paths.TempDirectory, "scene-processing-*"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            string package = Path.Combine(directory, "scene.pkg");
            if (File.Exists(package))
            {
                await RestoreAsync(new SceneWorkItem(
                    _paths.InputSceneFile,
                    package,
                    Path.Combine(directory, "project.json"),
                    directory), cancellationToken);
            }
            else CleanupDirectory(directory);
        }

        // Compatibilidade com versões anteriores que salvavam o pacote direto em temp.
        foreach (string path in Directory.EnumerateFiles(_paths.TempDirectory, "scene-processing-*.pkg"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await RestorePackageAsync(path, _paths.InputSceneFile, cancellationToken);
        }
    }

    public SceneWorkItem Claim(string inputPath)
    {
        string workDirectory = Path.Combine(_paths.TempDirectory, $"scene-processing-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workDirectory);
        string processingPath = Path.Combine(workDirectory, "scene.pkg");
        string projectPath = Path.Combine(workDirectory, "project.json");
        try
        {
            File.WriteAllText(projectPath, """
                {
                  "file": "scene.json",
                  "general": {
                    "properties": {}
                  },
                  "title": "Wallpaper Scene Exporter",
                  "type": "scene",
                  "version": 4
                }
                """);
            File.Move(inputPath, processingPath);
            return new SceneWorkItem(inputPath, processingPath, projectPath, workDirectory);
        }
        catch
        {
            if (!File.Exists(processingPath)) CleanupDirectory(workDirectory);
            throw;
        }
    }

    public async Task<string> RestoreAsync(SceneWorkItem item, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!File.Exists(item.ProcessingPath))
        {
            CleanupDirectory(item.WorkDirectory);
            return item.OriginalPath;
        }

        string destination = await RestorePackageAsync(item.ProcessingPath, item.OriginalPath, cancellationToken);
        CleanupDirectory(item.WorkDirectory);
        return destination;
    }

    public void Cleanup(SceneWorkItem item) => CleanupDirectory(item.WorkDirectory);

    private static Task<string> RestorePackageAsync(string processingPath, string originalPath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string destination = originalPath;
        if (File.Exists(destination))
        {
            string failedDirectory = Path.Combine(Path.GetDirectoryName(destination)!, "failed");
            Directory.CreateDirectory(failedDirectory);
            destination = Path.Combine(failedDirectory, $"scene_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}_{Guid.NewGuid():N}.pkg");
        }

        File.Move(processingPath, destination);
        return Task.FromResult(destination);
    }

    private void CleanupDirectory(string directory)
    {
        if (!Directory.Exists(directory)) return;
        string tempRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(_paths.TempDirectory));
        string target = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
        if (!target.StartsWith(tempRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("A pasta de trabalho está fora do diretório temporário.");
        Directory.Delete(target, recursive: true);
    }
}
