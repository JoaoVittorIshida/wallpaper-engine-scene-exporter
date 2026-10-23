using System.Text.Json;
using WallpaperSceneExporter.Helpers;
using WallpaperSceneExporter.Models;

namespace WallpaperSceneExporter.Services;

public sealed class ConfigService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    private readonly AppPaths _paths;

    public ConfigService(AppPaths paths) => _paths = paths;

    public AppConfig Current { get; private set; } = new();

    public async Task<AppConfig> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_paths.ConfigFile))
        {
            Current = new AppConfig();
            await SaveAsync(Current, cancellationToken);
            return Current;
        }

        try
        {
            await using FileStream stream = new(_paths.ConfigFile, FileMode.Open, FileAccess.Read, FileShare.Read);
            Current = await JsonSerializer.DeserializeAsync<AppConfig>(stream, JsonOptions, cancellationToken) ?? new AppConfig();
            Current.Validate();
            return Current;
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException)
        {
            string backup = _paths.ConfigFile + $".invalid-{DateTime.Now:yyyyMMdd-HHmmss}.bak";
            File.Copy(_paths.ConfigFile, backup, overwrite: false);
            Current = new AppConfig();
            await SaveAsync(Current, cancellationToken);
            return Current;
        }
    }

    public async Task SaveAsync(AppConfig config, CancellationToken cancellationToken = default)
    {
        config.Validate();
        string temporary = _paths.ConfigFile + ".tmp";
        await using (FileStream stream = new(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            await JsonSerializer.SerializeAsync(stream, config, JsonOptions, cancellationToken);
        File.Move(temporary, _paths.ConfigFile, true);
        Current = config.Clone();
    }
}
