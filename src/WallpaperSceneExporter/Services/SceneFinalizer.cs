using WallpaperSceneExporter.Models;

namespace WallpaperSceneExporter.Services;

public sealed class SceneFinalizer
{
    private readonly OutputValidationService _validator;

    public SceneFinalizer(OutputValidationService validator) => _validator = validator;

    public async Task<bool> DeleteInputAfterSuccessAsync(
        string scenePath,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        await _validator.ValidateOutputAsync(outputPath, cancellationToken);
        if (!File.Exists(scenePath)) return false;
        using (new FileStream(scenePath, FileMode.Open, FileAccess.Read, FileShare.None)) { }
        File.Delete(scenePath);
        return true;
    }
}
