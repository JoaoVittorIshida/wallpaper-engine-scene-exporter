using System.Text.Json.Serialization;

namespace WallpaperSceneExporter.Models;

public sealed class AppConfig
{
    public string OutputFormat { get; set; } = "gif";
    public string QualityPreset { get; set; } = "high";
    public int DurationSeconds { get; set; } = 10;
    public int WarmupSeconds { get; set; } = 2;
    public int CaptureFps { get; set; } = 60;
    public int Width { get; set; } = 480;
    public int Height { get; set; } = 480;
    public int? GifFps { get; set; }
    public int? GifColors { get; set; }
    public string? GifDithering { get; set; }
    public bool KeepFailedArtifacts { get; set; }
    public bool AutoProcess { get; set; }
    public string WallpaperEnginePath { get; set; } = string.Empty;
    public string FfmpegPath { get; set; } = string.Empty;

    [JsonIgnore]
    public QualityPreset Preset => Models.QualityPreset.FromName(QualityPreset);

    [JsonIgnore]
    public bool IsGifOutput => string.Equals(OutputFormat, "gif", StringComparison.OrdinalIgnoreCase);

    [JsonIgnore]
    public bool IsMp4Output => string.Equals(OutputFormat, "mp4", StringComparison.OrdinalIgnoreCase);

    [JsonIgnore]
    public int EffectiveGifFps => GifFps ?? Preset.GifFps;

    [JsonIgnore]
    public int EffectiveGifColors => GifColors ?? Preset.GifColors;

    [JsonIgnore]
    public string EffectiveGifDithering => string.IsNullOrWhiteSpace(GifDithering) ? "sierra2_4a" : GifDithering;

    public AppConfig Clone() => new()
    {
        OutputFormat = OutputFormat,
        QualityPreset = QualityPreset,
        DurationSeconds = DurationSeconds,
        WarmupSeconds = WarmupSeconds,
        CaptureFps = CaptureFps,
        Width = Width,
        Height = Height,
        GifFps = GifFps,
        GifColors = GifColors,
        GifDithering = GifDithering,
        KeepFailedArtifacts = KeepFailedArtifacts,
        AutoProcess = AutoProcess,
        WallpaperEnginePath = WallpaperEnginePath,
        FfmpegPath = FfmpegPath
    };

    public void Validate()
    {
        if (!IsGifOutput && !IsMp4Output)
            throw new InvalidDataException("O formato de saída deve ser GIF ou MP4.");
        if (DurationSeconds is < 1 or > 300) throw new InvalidDataException("A duração deve estar entre 1 e 300 segundos.");
        if (WarmupSeconds is < 0 or > 120) throw new InvalidDataException("O warm-up deve estar entre 0 e 120 segundos.");
        if (CaptureFps is < 1 or > 240) throw new InvalidDataException("O FPS de captura deve estar entre 1 e 240.");
        if ((long)Width * Height > 33_177_600) throw new InvalidDataException("A resolução excede o limite seguro de 8K.");
        if (Width is < 16 or > 7680 || Height is < 16 or > 4320) throw new InvalidDataException("A resolução configurada é inválida.");
        if (Width % 2 != 0 || Height % 2 != 0) throw new InvalidDataException("Largura e altura devem ser pares para H.264 yuv420p.");
        if (IsGifOutput)
        {
            if (EffectiveGifFps is < 1 or > 100) throw new InvalidDataException("O FPS do GIF deve estar entre 1 e 100.");
            if (EffectiveGifColors is < 2 or > 256) throw new InvalidDataException("O GIF deve usar entre 2 e 256 cores.");
            string[] dithers = ["bayer", "heckbert", "floyd_steinberg", "sierra2", "sierra2_4a", "sierra3", "burkes", "atkinson", "none"];
            if (!dithers.Contains(EffectiveGifDithering, StringComparer.OrdinalIgnoreCase))
                throw new InvalidDataException("Algoritmo de dithering inválido.");
        }
    }
}
