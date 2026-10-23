using System.Diagnostics;
using WallpaperSceneExporter.Helpers;
using WallpaperSceneExporter.Models;

namespace WallpaperSceneExporter.Services;

public sealed class FfmpegService : IFfmpegService
{
    private readonly AppPaths _paths;
    private readonly ProcessRunner _runner;
    private readonly LoggingService _log;

    public FfmpegService(AppPaths paths, ProcessRunner runner, LoggingService log)
    {
        _paths = paths;
        _runner = runner;
        _log = log;
    }

    public string ResolveExecutable(AppConfig config)
    {
        foreach (string candidate in GetCandidates(config.FfmpegPath))
            if (File.Exists(candidate)) return Path.GetFullPath(candidate);
        throw new FileNotFoundException("FFmpeg não foi encontrado. Coloque ffmpeg.exe na subpasta ffmpeg, no PATH, ou configure ffmpegPath.");
    }

    public Process StartRawEncoder(AppConfig config, string outputPath, int sourceWidth, int sourceHeight)
    {
        string ffmpeg = ResolveExecutable(config);
        return _runner.StartWithInput(ffmpeg, FfmpegCommandBuilder.BuildRawVideoEncodeArguments(config, outputPath, sourceWidth, sourceHeight));
    }

    public string ResolveProbeExecutable(AppConfig config)
    {
        string candidate = Path.Combine(Path.GetDirectoryName(ResolveExecutable(config))!, "ffprobe.exe");
        if (File.Exists(candidate)) return candidate;
        throw new FileNotFoundException("ffprobe.exe não foi encontrado ao lado de ffmpeg.exe. Instale o pacote completo do FFmpeg.");
    }

    public async Task<string> ValidateAsync(AppConfig config, CancellationToken cancellationToken = default)
    {
        string executable = ResolveExecutable(config);
        string probe = ResolveProbeExecutable(config);
        await _runner.CaptureOutputAsync(probe, ["-hide_banner", "-version"], TimeSpan.FromSeconds(10), cancellationToken);
        string version = await _runner.CaptureOutputAsync(executable, ["-hide_banner", "-version"], TimeSpan.FromSeconds(10), cancellationToken);
        string capabilities = await _runner.CaptureOutputAsync(executable, ["-hide_banner", "-filters"], TimeSpan.FromSeconds(15), cancellationToken);
        string encoders = await _runner.CaptureOutputAsync(executable, ["-hide_banner", "-encoders"], TimeSpan.FromSeconds(15), cancellationToken);
        if (!capabilities.Contains("palettegen", StringComparison.Ordinal) || !capabilities.Contains("paletteuse", StringComparison.Ordinal))
            throw new InvalidDataException("Esta versão do FFmpeg não inclui os filtros palettegen/paletteuse.");
        if (!encoders.Contains("libx264", StringComparison.Ordinal))
            throw new InvalidDataException("Esta versão do FFmpeg não inclui o encoder H.264 libx264.");
        string firstLine = version.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "FFmpeg";
        int copyright = firstLine.IndexOf(" Copyright", StringComparison.OrdinalIgnoreCase);
        if (copyright > 0) firstLine = firstLine[..copyright];
        return firstLine;
    }

    public async Task GenerateGifAsync(AppConfig config, string mp4Path, string palettePath, string gifPath, CancellationToken cancellationToken)
    {
        string ffmpeg = ResolveExecutable(config);
        await _log.InfoAsync("Gerando paleta GIF", cancellationToken);
        await _runner.RunAsync(ffmpeg, FfmpegCommandBuilder.BuildPaletteArguments(config, mp4Path, palettePath), timeout: TimeSpan.FromMinutes(5), cancellationToken: cancellationToken);
        await _log.InfoAsync("Codificando GIF", cancellationToken);
        await _runner.RunAsync(ffmpeg, FfmpegCommandBuilder.BuildGifArguments(config, mp4Path, palettePath, gifPath), timeout: TimeSpan.FromMinutes(10), cancellationToken: cancellationToken);
    }

    private IEnumerable<string> GetCandidates(string configured)
    {
        if (!string.IsNullOrWhiteSpace(configured))
        {
            string path = Environment.ExpandEnvironmentVariables(configured.Trim().Trim('"'));
            yield return Directory.Exists(path) ? Path.Combine(path, "ffmpeg.exe") : path;
        }
        yield return Path.Combine(_paths.BaseDirectory, "ffmpeg", "ffmpeg.exe");

        string? pathValue = Environment.GetEnvironmentVariable("PATH");
        if (pathValue is null) yield break;
        foreach (string folder in pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            yield return Path.Combine(folder.Trim('"'), "ffmpeg.exe");
    }
}
