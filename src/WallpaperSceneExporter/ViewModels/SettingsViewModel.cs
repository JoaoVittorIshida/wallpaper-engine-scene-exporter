using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WallpaperSceneExporter.Models;
using WallpaperSceneExporter.Services;
using Microsoft.Win32;

namespace WallpaperSceneExporter.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly ConfigService _configService;

    [ObservableProperty] private string outputFormat;
    [ObservableProperty] private string qualityPreset;
    [ObservableProperty] private string durationSeconds;
    [ObservableProperty] private string warmupSeconds;
    [ObservableProperty] private string captureFps;
    [ObservableProperty] private string width;
    [ObservableProperty] private string height;
    [ObservableProperty] private string gifFps;
    [ObservableProperty] private string gifColors;
    [ObservableProperty] private string gifDithering;
    [ObservableProperty] private bool keepFailedArtifacts;
    [ObservableProperty] private bool autoProcess;
    [ObservableProperty] private string wallpaperEnginePath;
    [ObservableProperty] private string ffmpegPath;
    [ObservableProperty] private string errorMessage = string.Empty;

    public SettingsViewModel(ConfigService configService)
    {
        _configService = configService;
        AppConfig config = configService.Current;
        outputFormat = config.OutputFormat.ToUpperInvariant();
        qualityPreset = config.QualityPreset;
        durationSeconds = config.DurationSeconds.ToString();
        warmupSeconds = config.WarmupSeconds.ToString();
        captureFps = config.CaptureFps.ToString();
        width = config.Width.ToString();
        height = config.Height.ToString();
        gifFps = config.GifFps?.ToString() ?? string.Empty;
        gifColors = config.GifColors?.ToString() ?? string.Empty;
        gifDithering = config.GifDithering ?? string.Empty;
        keepFailedArtifacts = config.KeepFailedArtifacts;
        autoProcess = config.AutoProcess;
        wallpaperEnginePath = config.WallpaperEnginePath;
        ffmpegPath = config.FfmpegPath;
    }

    public IReadOnlyList<string> Presets { get; } = ["compact", "high", "maximum"];
    public IReadOnlyList<string> DitheringOptions { get; } = ["sierra2_4a", "sierra3", "floyd_steinberg", "bayer", "atkinson", "none"];
    public event EventHandler<bool>? CloseRequested;

    public bool IsGifOutput
    {
        get => string.Equals(OutputFormat, "GIF", StringComparison.OrdinalIgnoreCase);
        set { if (value) OutputFormat = "GIF"; }
    }

    public bool IsMp4Output
    {
        get => string.Equals(OutputFormat, "MP4", StringComparison.OrdinalIgnoreCase);
        set { if (value) OutputFormat = "MP4"; }
    }

    public string QualityHint => IsGifOutput
        ? "Equilibrada é indicada para a maioria."
        : "Vídeo usa H.264 em alta qualidade. Este ajuste se aplica apenas ao GIF.";

    partial void OnOutputFormatChanged(string value)
    {
        OnPropertyChanged(nameof(IsGifOutput));
        OnPropertyChanged(nameof(IsMp4Output));
        OnPropertyChanged(nameof(QualityHint));
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        try
        {
            var config = new AppConfig
            {
                OutputFormat = OutputFormat.ToLowerInvariant(),
                QualityPreset = QualityPreset,
                DurationSeconds = ParseRequired(DurationSeconds, "Duração"),
                WarmupSeconds = ParseRequired(WarmupSeconds, "Warm-up"),
                CaptureFps = ParseRequired(CaptureFps, "FPS de captura"),
                Width = ParseRequired(Width, "Largura"),
                Height = ParseRequired(Height, "Altura"),
                GifFps = IsGifOutput ? ParseOptional(GifFps, "FPS do GIF") : _configService.Current.GifFps,
                GifColors = IsGifOutput ? ParseOptional(GifColors, "Cores do GIF") : _configService.Current.GifColors,
                GifDithering = IsGifOutput
                    ? (string.IsNullOrWhiteSpace(GifDithering) ? null : GifDithering)
                    : _configService.Current.GifDithering,
                KeepFailedArtifacts = KeepFailedArtifacts,
                AutoProcess = AutoProcess,
                WallpaperEnginePath = WallpaperEnginePath.Trim(),
                FfmpegPath = FfmpegPath.Trim()
            };
            await _configService.SaveAsync(config);
            CloseRequested?.Invoke(this, true);
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    [RelayCommand] private void Cancel() => CloseRequested?.Invoke(this, false);

    [RelayCommand]
    private void BrowseWallpaperEngine()
    {
        string? selected = BrowseExecutable("Selecionar wallpaper64.exe", "wallpaper64.exe|wallpaper64.exe|Executáveis (*.exe)|*.exe");
        if (selected is not null) WallpaperEnginePath = selected;
    }

    [RelayCommand]
    private void BrowseFfmpeg()
    {
        string? selected = BrowseExecutable("Selecionar ffmpeg.exe", "ffmpeg.exe|ffmpeg.exe|Executáveis (*.exe)|*.exe");
        if (selected is not null) FfmpegPath = selected;
    }

    private static string? BrowseExecutable(string title, string filter)
    {
        var dialog = new OpenFileDialog { Title = title, Filter = filter, CheckFileExists = true, Multiselect = false };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    private static int ParseRequired(string value, string field) =>
        int.TryParse(value, out int parsed) ? parsed : throw new InvalidDataException($"{field} deve ser um número inteiro.");

    private static int? ParseOptional(string value, string field) =>
        string.IsNullOrWhiteSpace(value) ? null : ParseRequired(value, field);
}
