using WallpaperSceneExporter.Helpers;
using WallpaperSceneExporter.Models;

namespace WallpaperSceneExporter.Services;

public sealed class ConversionService : ISceneConversionService
{
    private readonly AppPaths _paths;
    private readonly ConfigService _config;
    private readonly FileStabilityService _stability;
    private readonly IWallpaperEngineService _wallpaperEngine;
    private readonly IWindowCaptureService _capture;
    private readonly IFfmpegService _ffmpeg;
    private readonly OutputValidationService _validation;
    private readonly SceneFinalizer _finalizer;
    private readonly SceneWorkItemService _workItems;
    private readonly ILoggingService _log;
    private readonly SemaphoreSlim _singleConversion = new(1, 1);

    public ConversionService(
        AppPaths paths,
        ConfigService config,
        FileStabilityService stability,
        IWallpaperEngineService wallpaperEngine,
        IWindowCaptureService capture,
        IFfmpegService ffmpeg,
        OutputValidationService validation,
        SceneFinalizer finalizer,
        SceneWorkItemService workItems,
        ILoggingService log)
    {
        _paths = paths;
        _config = config;
        _stability = stability;
        _wallpaperEngine = wallpaperEngine;
        _capture = capture;
        _ffmpeg = ffmpeg;
        _validation = validation;
        _finalizer = finalizer;
        _workItems = workItems;
        _log = log;
    }

    public async Task<ConversionResult> ProcessSceneAsync(string scenePath, IProgress<PipelineProgress> progress, CancellationToken cancellationToken)
    {
        await _singleConversion.WaitAsync(cancellationToken);
        var run = new RunState();
        try
        {
            ConversionResult result = await ProcessCoreAsync(scenePath, progress, run, cancellationToken);
            return result with { HasPendingInput = run.HasPendingInput };
        }
        finally { _singleConversion.Release(); }
    }

    private async Task<ConversionResult> ProcessCoreAsync(string scenePath, IProgress<PipelineProgress> progress, RunState run, CancellationToken cancellationToken)
    {
        string? wallpaperExecutable = null;
        string? windowName = null;
        string runId = Guid.NewGuid().ToString("N");
        string mp4Path = Path.Combine(_paths.TempDirectory, $"capture_{runId}.mp4");
        string palettePath = Path.Combine(_paths.TempDirectory, $"palette_{runId}.png");
        string gifPath = Path.Combine(_paths.TempDirectory, $"result_{runId}.gif");
        string? outputPath = null;
        bool success = false;
        SceneWorkItem? workItem = null;
        AppConfig? runConfig = null;
        var states = new PipelineStateMachine();
        void Report(PipelineState state, string message, double percent)
        {
            states.MoveTo(state);
            try { progress.Report(new(state, message, percent)); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
        }

        try
        {
            AppConfig config = _config.Current.Clone();
            runConfig = config;
            config.Validate();
            Report(PipelineState.Preparing, "Preparando sua cena…", 0.03);
            await TryLogAsync("scene.pkg detectado");
            await _stability.WaitUntilReadyAsync(scenePath, cancellationToken: cancellationToken);
            await TryLogAsync("Arquivo liberado para leitura");

            Report(PipelineState.Preparing, "Organizando tudo…", 0.06);
            await _ffmpeg.ValidateAsync(config, cancellationToken);
            wallpaperExecutable = await _wallpaperEngine.EnsureAvailableAsync(config, cancellationToken);
            EnsureDiskSpace(config);
            workItem = _workItems.Claim(scenePath);
            scenePath = workItem.ProcessingPath;
            windowName = $"WSE_Render_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";

            Report(PipelineState.Rendering, "Abrindo a cena…", 0.10);
            await TryLogAsync("Abrindo scene.pkg");
            await TryLogAsync($"Projeto de renderização: {workItem.RendererProjectPath}");
            await _wallpaperEngine.OpenSceneAsync(wallpaperExecutable, workItem.RendererProjectPath, windowName, config, cancellationToken);
            nint hwnd = await _wallpaperEngine.WaitForWindowAsync(windowName, config.Width, config.Height, cancellationToken);
            await TryLogAsync($"Janela encontrada HWND=0x{hwnd:X}");

            if (config.WarmupSeconds > 0)
                await Task.Delay(TimeSpan.FromSeconds(config.WarmupSeconds), cancellationToken);
            await TryLogAsync("Warm-up concluído");

            Report(PipelineState.Recording, "Criando seu arquivo…", 0.18);
            await TryLogAsync($"Iniciando captura {config.Width}x{config.Height} @ {config.CaptureFps} FPS");
            var captureProgress = new InlineProgress<double>(value =>
                Report(PipelineState.Recording, $"Gravando… {value:P0}", 0.18 + value * 0.52));
            await _capture.CaptureAsync(hwnd, config, mp4Path, captureProgress, cancellationToken);

            await _wallpaperEngine.CloseWindowAsync(wallpaperExecutable, windowName);
            windowName = null;

            if (config.IsMp4Output)
            {
                Report(PipelineState.Converting, "Finalizando o vídeo…", 0.90);
                outputPath = OutputNameGenerator.CreateUniquePath(_paths.OutputDirectory, ".mp4");
                await _validation.ValidateMp4Async(mp4Path, config, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                File.Move(mp4Path, outputPath);
                await TryLogAsync($"MP4 criado: {outputPath}");
            }
            else
            {
                Report(PipelineState.Converting, "Ajustando as cores…", 0.73);
                outputPath = OutputNameGenerator.CreateUniquePath(_paths.OutputDirectory, ".gif");
                await _ffmpeg.GenerateGifAsync(config, mp4Path, palettePath, gifPath, cancellationToken);
                Report(PipelineState.Converting, "Finalizando a animação…", 0.94);
                await _validation.ValidateGifAsync(gifPath, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                File.Move(gifPath, outputPath);
                await TryLogAsync($"GIF criado: {outputPath}");
            }

            bool inputDeleted = await _finalizer.DeleteInputAfterSuccessAsync(scenePath, outputPath, cancellationToken);
            if (!inputDeleted) throw new IOException("O arquivo de entrada processado não pôde ser finalizado.");
            // Commit point: there must be no fallible operation between deletion and this flag.
            // A validated, published result is never rolled back by logging or notifications.
            success = true;
            await TryLogAsync("scene.pkg removido");
            Report(PipelineState.Completed, "Seu arquivo está pronto", 1);
            await TryLogAsync("Conversão concluída");
            return ConversionResult.Ok(outputPath);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (success) return ConversionResult.Ok(outputPath!);
            await TryLogAsync("Conversão cancelada; a entrada será recuperada.", error: true);
            throw;
        }
        catch (Exception ex)
        {
            if (success) return ConversionResult.Ok(outputPath!);
            Report(PipelineState.Error, ex.Message, 0);
            await TryLogAsync($"{ex.Message}{Environment.NewLine}{ex}", error: true);
            return ConversionResult.Failed(ex.Message);
        }
        finally
        {
            if (workItem is not null)
                run.HasPendingInput = File.Exists(workItem.OriginalPath);
            if (wallpaperExecutable is not null && windowName is not null)
                await TryCleanupAsync(() => _wallpaperEngine.CloseWindowAsync(wallpaperExecutable, windowName));
            if (!success && workItem is not null && File.Exists(workItem.ProcessingPath))
            {
                await TryCleanupAsync(async () =>
                {
                    string restored = await _workItems.RestoreAsync(workItem, CancellationToken.None);
                    run.HasPendingInput |= !string.Equals(restored, workItem.OriginalPath, StringComparison.OrdinalIgnoreCase);
                    await TryLogAsync($"Entrada preservada em: {restored}");
                });
            }
            // Never delete a work directory if restoration failed and its package is still there.
            if (workItem is not null && !File.Exists(workItem.ProcessingPath))
                await TryCleanupAsync(() => { _workItems.Cleanup(workItem); return Task.CompletedTask; });
            if (success || runConfig?.KeepFailedArtifacts != true)
            {
                TryDelete(palettePath);
                TryDelete(mp4Path);
                TryDelete(gifPath);
            }
        }
    }

    private async Task TryLogAsync(string message, bool error = false)
    {
        try
        {
            if (error) await _log.ErrorAsync(message);
            else await _log.InfoAsync(message);
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
    }

    private async Task TryCleanupAsync(Func<Task> cleanup)
    {
        try { await cleanup(); }
        catch (Exception ex)
        {
            await TryLogAsync($"Falha na recuperação/limpeza; arquivos remanescentes serão recuperados na próxima abertura: {ex.Message}", error: true);
        }
    }

    private void EnsureDiskSpace(AppConfig config)
    {
        string root = Path.GetPathRoot(_paths.TempDirectory) ?? _paths.TempDirectory;
        var drive = new DriveInfo(root);
        long estimated = Math.Max(256L * 1024 * 1024,
            (long)config.Width * config.Height * config.DurationSeconds * Math.Max(8, config.EffectiveGifFps));
        if (drive.AvailableFreeSpace < estimated)
            throw new IOException($"Espaço em disco insuficiente. Libere pelo menos {estimated / 1024 / 1024:N0} MB.");
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }

    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }

    private sealed class RunState
    {
        public bool HasPendingInput { get; set; }
    }
}
