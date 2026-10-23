using System.Diagnostics;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using WallpaperSceneExporter.Helpers;
using WallpaperSceneExporter.Models;
using WallpaperSceneExporter.Services;
using WallpaperSceneExporter.Views;

namespace WallpaperSceneExporter.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly AppPaths _paths;
    private readonly ConfigService _config;
    private readonly ExporterController _controller;
    private readonly DependencyService _dependencies;

    [ObservableProperty] private PipelineState state = PipelineState.Waiting;
    [ObservableProperty] private string statusText = "Escolha uma cena para começar";
    [ObservableProperty] private double progress;
    [ObservableProperty] private string lastFile = "Nenhum resultado ainda";
    [ObservableProperty] private bool hasInput;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private bool dependenciesReady;
    [ObservableProperty] private bool isCheckingDependencies;
    [ObservableProperty] private string ffmpegStatus = "Verificando…";
    [ObservableProperty] private string wallpaperStatus = "Verificando…";
    [ObservableProperty] private string captureStatus = "Verificando…";
    [ObservableProperty] private bool ffmpegReady;
    [ObservableProperty] private bool wallpaperReady;
    [ObservableProperty] private bool captureReady;

    public MainViewModel(AppPaths paths, ConfigService config, ExporterController controller, DependencyService dependencies)
    {
        _paths = paths;
        _config = config;
        _controller = controller;
        _dependencies = dependencies;
        hasInput = controller.HasInput;
        _controller.ProgressChanged += OnProgressChanged;
        _controller.ConversionFinished += OnConversionFinished;
        _controller.InputAvailabilityChanged += (_, value) => Dispatch(() => HasInput = value);
        _controller.BusyChanged += (_, value) => Dispatch(() => IsBusy = value);
        _controller.ControllerError += (_, message) => Dispatch(() => StatusText = message);
    }

    public string InputPath => _paths.InputDirectory;
    public string WindowTitle => $"Wallpaper Engine Scene Exporter · v{typeof(MainViewModel).Assembly.GetName().Version?.ToString(3)}";
    public string OutputPath => _paths.OutputDirectory;
    public string InputFileText => HasInput ? "Cena pronta" : "Solte sua cena aqui";
    public string PrimaryActionText => State == PipelineState.Error
        ? "Tentar novamente"
        : _config.Current.IsMp4Output ? "Criar vídeo" : "Criar GIF";
    public string PresetText => _config.Current.IsMp4Output
        ? "Vídeo"
        : _config.Current.Preset.Name switch
        {
            "compact" => "GIF · arquivo menor",
            "maximum" => "GIF · mais detalhes",
            _ => "GIF · equilibrado"
        };
    public string CaptureSummary => $"{_config.Current.DurationSeconds} segundos · {_config.Current.Width} × {_config.Current.Height}";
    public bool CanStart => HasInput && DependenciesReady && !IsBusy && !IsCheckingDependencies;
    public bool CanCancel => IsBusy;

    partial void OnHasInputChanged(bool value)
    {
        OnPropertyChanged(nameof(InputFileText));
        OnPropertyChanged(nameof(CanStart));
        if (!IsBusy && State != PipelineState.Error)
            StatusText = value ? "Tudo pronto para criar" : "Escolha uma cena para começar";
    }

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(CanStart));
        OnPropertyChanged(nameof(CanCancel));
        OnPropertyChanged(nameof(PrimaryActionText));
    }

    partial void OnDependenciesReadyChanged(bool value) => OnPropertyChanged(nameof(CanStart));
    partial void OnIsCheckingDependenciesChanged(bool value) => OnPropertyChanged(nameof(CanStart));

    [RelayCommand] private void OpenInput() => OpenFolder(_paths.InputDirectory);
    [RelayCommand] private void OpenOutput() => OpenFolder(_paths.OutputDirectory);

    [RelayCommand]
    private void Start()
    {
        if (!_controller.RequestStart()) return;
        StatusText = "Preparando sua cena…";
    }

    [RelayCommand] private void Cancel() => _controller.Cancel();
    [RelayCommand] private void Retry() => Start();

    [RelayCommand]
    private async Task ChooseSceneAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Escolher cena",
            Filter = "Arquivos de cena (*.pkg)|*.pkg|Todos os arquivos (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog(Application.Current.MainWindow) == true)
        {
            try { await ImportSceneAsync(dialog.FileName); }
            catch (Exception ex) { ReportUiError(ex.Message); }
        }
    }

    public async Task ImportSceneAsync(string sourcePath)
    {
        if (IsBusy) throw new InvalidOperationException("Aguarde ou cancele antes de trocar a cena.");
        if (!File.Exists(sourcePath)) throw new FileNotFoundException("O arquivo selecionado não existe.", sourcePath);
        if (!string.Equals(Path.GetExtension(sourcePath), ".pkg", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Escolha um arquivo de cena válido.");
        if (File.Exists(_paths.InputSceneFile))
            throw new IOException("Já existe uma cena aguardando. Conclua a conversão atual antes de trocar.");

        string incoming = Path.Combine(_paths.InputDirectory, $".incoming-{Guid.NewGuid():N}.tmp");
        try
        {
            await using FileStream source = new(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            await using (FileStream destination = new(incoming, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                await source.CopyToAsync(destination);
            File.Move(incoming, _paths.InputSceneFile);
            HasInput = true;
            StatusText = "Tudo pronto para criar";
        }
        finally
        {
            try { if (File.Exists(incoming)) File.Delete(incoming); } catch { }
        }
    }

    public void ReportUiError(string message)
    {
        State = PipelineState.Error;
        StatusText = message;
        OnPropertyChanged(nameof(PrimaryActionText));
    }

    [RelayCommand]
    private async Task RefreshDependenciesAsync()
    {
        IsCheckingDependencies = true;
        try
        {
            DependencyStatus status = await _dependencies.CheckAsync(_config.Current);
            FfmpegReady = status.FfmpegReady;
            WallpaperReady = status.WallpaperEngineReady;
            CaptureReady = status.CaptureReady;
            FfmpegStatus = status.FfmpegMessage;
            WallpaperStatus = status.WallpaperEngineMessage;
            CaptureStatus = status.CaptureMessage;
            DependenciesReady = status.AllReady;
            if (!IsBusy)
                StatusText = status.AllReady
                    ? (HasInput ? "Tudo pronto para criar" : "Escolha uma cena para começar")
                    : "O aplicativo precisa de atenção. Confira as configurações.";
        }
        finally { IsCheckingDependencies = false; }
    }

    [RelayCommand]
    private async Task OpenSettingsAsync()
    {
        var viewModel = new SettingsViewModel(_config);
        var window = new SettingsWindow { DataContext = viewModel, Owner = Application.Current.MainWindow };
        viewModel.CloseRequested += (_, saved) => { window.DialogResult = saved; window.Close(); };
        bool? saved = window.ShowDialog();
        if (saved == true)
        {
            OnPropertyChanged(nameof(PresetText));
            OnPropertyChanged(nameof(CaptureSummary));
            OnPropertyChanged(nameof(PrimaryActionText));
            await RefreshDependenciesAsync();
        }
    }

    private void OnProgressChanged(object? sender, PipelineProgress update) => Dispatch(() =>
    {
        State = update.State;
        StatusText = update.Message;
        Progress = update.Percent * 100;
        OnPropertyChanged(nameof(PrimaryActionText));
    });

    private void OnConversionFinished(object? sender, ConversionResult result) => Dispatch(() =>
    {
        if (result.Success && result.OutputPath is not null) LastFile = Path.GetFileName(result.OutputPath);
        OnPropertyChanged(nameof(PrimaryActionText));
    });

    private static void Dispatch(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.HasShutdownStarted) return;
        if (dispatcher.CheckAccess()) action();
        else dispatcher.BeginInvoke(action);
    }

    private static void OpenFolder(string path) => Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
}
