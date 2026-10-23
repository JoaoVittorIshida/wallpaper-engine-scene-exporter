using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using WallpaperSceneExporter.Helpers;
using WallpaperSceneExporter.Models;

namespace WallpaperSceneExporter.Services;

public sealed partial class WallpaperEngineService : IWallpaperEngineService
{
    private readonly ProcessRunner _runner;
    private readonly LoggingService _log;

    public WallpaperEngineService(ProcessRunner runner, LoggingService log)
    {
        _runner = runner;
        _log = log;
    }

    public string ResolveExecutable(AppConfig config)
    {
        foreach (string candidate in GetCandidates(config.WallpaperEnginePath).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!File.Exists(candidate)) continue;
            string preferred = Path.GetFullPath(candidate);
            string installationDirectory = Path.GetDirectoryName(preferred)!;
            return FindRunningExecutable(installationDirectory) ?? preferred;
        }
        throw new FileNotFoundException("Wallpaper Engine não foi encontrado. Configure wallpaperEnginePath no config.json.");
    }

    public async Task<string> EnsureAvailableAsync(AppConfig config, CancellationToken cancellationToken)
    {
        string executable = ResolveExecutable(config);
        string installationDirectory = Path.GetDirectoryName(executable)!;
        string? runningExecutable = FindRunningExecutable(installationDirectory);
        if (runningExecutable is not null)
        {
            await _log.InfoAsync($"Wallpaper Engine ativo: {runningExecutable}", cancellationToken);
            return runningExecutable;
        }

        await _log.InfoAsync($"Iniciando Wallpaper Engine: {executable}", cancellationToken);
        if (IsProcessRunningFrom(executable))
            return executable;

        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            WorkingDirectory = Path.GetDirectoryName(executable)!,
            UseShellExecute = false,
            WindowStyle = ProcessWindowStyle.Hidden
        };
        Process.Start(startInfo)?.Dispose();

        DateTimeOffset deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(20);
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            runningExecutable = FindRunningExecutable(installationDirectory);
            if (runningExecutable is not null)
            {
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
                return runningExecutable;
            }
            await Task.Delay(250, cancellationToken);
        }
        throw new TimeoutException("Wallpaper Engine não ficou pronto dentro de 20 segundos.");
    }

    public async Task OpenSceneAsync(string executable, string scenePath, string windowName, AppConfig config, CancellationToken cancellationToken)
    {
        string[] arguments =
        [
            "-control", "openWallpaper", "-file", scenePath,
            "-playInWindow", windowName,
            "-width", config.Width.ToString(), "-height", config.Height.ToString(), "-borderless"
        ];
        await _runner.RunAsync(executable, arguments, timeout: TimeSpan.FromSeconds(30), cancellationToken: cancellationToken);
    }

    public async Task<nint> WaitForWindowAsync(string windowName, int expectedWidth, int expectedHeight, CancellationToken cancellationToken)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(30);
        (int Width, int Height) previousSize = default;
        int stableSamples = 0;
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            nint hwnd = NativeWindow.FindTopLevelWindow(windowName);
            if (hwnd != 0)
            {
                (int width, int height) = NativeWindow.GetClientSize(hwnd);
                if (width > 0 && height > 0)
                {
                    if (width == expectedWidth && height == expectedHeight)
                        return hwnd;

                    (int Width, int Height) currentSize = (width, height);
                    stableSamples = currentSize == previousSize ? stableSamples + 1 : 1;
                    previousSize = currentSize;
                    if (stableSamples >= 3)
                    {
                        await _log.InfoAsync(
                            $"Janela criada em {width}x{height} pixels físicos; a saída será ajustada para {expectedWidth}x{expectedHeight}.",
                            cancellationToken);
                        return hwnd;
                    }
                }
            }
            await Task.Delay(200, cancellationToken);
        }
        throw new TimeoutException($"A janela de renderização {windowName} não ficou pronta em 30 segundos.");
    }

    public async Task CloseWindowAsync(string executable, string windowName, CancellationToken cancellationToken = default)
    {
        try
        {
            await _runner.RunAsync(executable, ["-control", "closeWallpaper", "-location", windowName], timeout: TimeSpan.FromSeconds(10), cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            await _log.ErrorAsync($"Falha ao fechar a janela temporária: {ex.Message}");
        }
    }

    private IEnumerable<string> GetCandidates(string configured)
    {
        if (!string.IsNullOrWhiteSpace(configured))
        {
            string configuredPath = Environment.ExpandEnvironmentVariables(configured.Trim().Trim('"'));
            if (Directory.Exists(configuredPath))
            {
                yield return Path.Combine(configuredPath, "wallpaper64.exe");
                yield return Path.Combine(configuredPath, "wallpaper32.exe");
            }
            else
            {
                yield return configuredPath;
                string? directory = Path.GetDirectoryName(configuredPath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    string sibling = string.Equals(Path.GetFileName(configuredPath), "wallpaper64.exe", StringComparison.OrdinalIgnoreCase)
                        ? "wallpaper32.exe"
                        : "wallpaper64.exe";
                    yield return Path.Combine(directory, sibling);
                }
            }
        }

        yield return @"C:\Program Files (x86)\Steam\steamapps\common\wallpaper_engine\wallpaper64.exe";
        yield return @"C:\Program Files (x86)\Steam\steamapps\common\wallpaper_engine\wallpaper32.exe";
        foreach (string library in DiscoverSteamLibraries())
        {
            yield return Path.Combine(library, "steamapps", "common", "wallpaper_engine", "wallpaper64.exe");
            yield return Path.Combine(library, "steamapps", "common", "wallpaper_engine", "wallpaper32.exe");
        }
    }

    private static string? FindRunningExecutable(string installationDirectory)
    {
        string normalizedDirectory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(installationDirectory));
        foreach (string processName in new[] { "wallpaper64", "wallpaper32" })
        {
            foreach (Process process in Process.GetProcessesByName(processName))
            {
                using (process)
                {
                    try
                    {
                        string? path = process.MainModule?.FileName;
                        if (path is not null && string.Equals(
                                Path.TrimEndingDirectorySeparator(Path.GetDirectoryName(Path.GetFullPath(path))!),
                                normalizedDirectory,
                                StringComparison.OrdinalIgnoreCase))
                            return Path.GetFullPath(path);
                    }
                    catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
                    {
                        // O processo pode encerrar ou negar acesso entre a enumeração e a leitura.
                    }
                }
            }
        }
        return null;
    }

    private static bool IsProcessRunningFrom(string executable)
    {
        string? running = FindRunningExecutable(Path.GetDirectoryName(Path.GetFullPath(executable))!);
        return running is not null && string.Equals(running, Path.GetFullPath(executable), StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> DiscoverSteamLibraries()
    {
        var steamRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
        {
            if (key?.GetValue("SteamPath") is string path) steamRoots.Add(path.Replace('/', '\\'));
        }
        steamRoots.Add(@"C:\Program Files (x86)\Steam");

        foreach (string root in steamRoots.ToArray())
        {
            yield return root;
            string vdf = Path.Combine(root, "steamapps", "libraryfolders.vdf");
            if (!File.Exists(vdf)) continue;
            string text;
            try { text = File.ReadAllText(vdf); }
            catch { continue; }
            foreach (Match match in VdfPathRegex().Matches(text))
                yield return match.Groups[1].Value.Replace("\\\\", "\\");
        }
    }

    [GeneratedRegex("\\\"path\\\"\\s+\\\"([^\\\"]+)\\\"", RegexOptions.IgnoreCase)]
    private static partial Regex VdfPathRegex();
}
