using WallpaperSceneExporter.Helpers;
using WallpaperSceneExporter.Services;

namespace WallpaperSceneExporter.Tests;

public sealed class SceneWorkItemServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "wse-workitem-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task ANewInputNeverOverwritesTheFailedOriginal()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var paths = new AppPaths(_directory);
        paths.EnsureDirectories();
        await File.WriteAllTextAsync(paths.InputSceneFile, "original", cancellationToken);
        var service = new SceneWorkItemService(paths);
        var item = service.Claim(paths.InputSceneFile);
        Assert.Equal("scene.pkg", Path.GetFileName(item.ProcessingPath));
        Assert.Equal("project.json", Path.GetFileName(item.RendererProjectPath));
        Assert.True(File.Exists(item.RendererProjectPath));
        Assert.Contains("\"file\": \"scene.json\"", await File.ReadAllTextAsync(item.RendererProjectPath, cancellationToken));
        await File.WriteAllTextAsync(paths.InputSceneFile, "new", cancellationToken);

        string restored = await service.RestoreAsync(item, cancellationToken);

        Assert.Equal("new", await File.ReadAllTextAsync(paths.InputSceneFile, cancellationToken));
        Assert.Contains(Path.Combine("input", "failed"), restored);
        Assert.Equal("original", await File.ReadAllTextAsync(restored, cancellationToken));
        Assert.False(Directory.Exists(item.WorkDirectory));
    }

    [Fact]
    public async Task InterruptedDirectoryWorkIsRecoveredOnStartup()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var paths = new AppPaths(_directory);
        paths.EnsureDirectories();
        string workDirectory = Path.Combine(paths.TempDirectory, "scene-processing-interrupted");
        Directory.CreateDirectory(workDirectory);
        await File.WriteAllTextAsync(Path.Combine(workDirectory, "scene.pkg"), "preserved", cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(workDirectory, "project.json"), "{}", cancellationToken);

        await new SceneWorkItemService(paths).RecoverInterruptedWorkAsync(cancellationToken);

        Assert.Equal("preserved", await File.ReadAllTextAsync(paths.InputSceneFile, cancellationToken));
        Assert.False(Directory.Exists(workDirectory));
    }

    [Fact]
    public async Task InterruptedWorkIsRecoveredOnStartup()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var paths = new AppPaths(_directory);
        paths.EnsureDirectories();
        string orphan = Path.Combine(paths.TempDirectory, "scene-processing-orphan.pkg");
        await File.WriteAllTextAsync(orphan, "preserved", cancellationToken);

        await new SceneWorkItemService(paths).RecoverInterruptedWorkAsync(cancellationToken);

        Assert.Equal("preserved", await File.ReadAllTextAsync(paths.InputSceneFile, cancellationToken));
        Assert.False(File.Exists(orphan));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }
}
