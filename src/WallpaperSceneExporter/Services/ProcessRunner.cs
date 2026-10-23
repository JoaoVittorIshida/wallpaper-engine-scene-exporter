using System.Diagnostics;

namespace WallpaperSceneExporter.Services;

public sealed class ProcessRunner
{
    public async Task RunAsync(
        string executable,
        IReadOnlyList<string> arguments,
        Action<string>? stderrLine = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        using Process process = CreateProcess(executable, arguments, redirectInput: false);
        var errors = new List<string>();
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            stderrLine?.Invoke(e.Data);
            lock (errors)
            {
                if (errors.Count >= 30) errors.RemoveAt(0);
                errors.Add(e.Data);
            }
        };
        if (!process.Start()) throw new InvalidOperationException($"Não foi possível iniciar {Path.GetFileName(executable)}.");
        process.BeginErrorReadLine();

        using var timeoutSource = timeout is null ? null : new CancellationTokenSource(timeout.Value);
        using var linkedSource = timeoutSource is null
            ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
            : CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token);
        try
        {
            await process.WaitForExitAsync(linkedSource.Token);
        }
        catch (OperationCanceledException) when (timeoutSource?.IsCancellationRequested == true && !cancellationToken.IsCancellationRequested)
        {
            TryKill(process);
            throw new TimeoutException($"{Path.GetFileName(executable)} excedeu o limite de {timeout:hh\\:mm\\:ss}.");
        }
        catch
        {
            TryKill(process);
            throw;
        }

        if (process.ExitCode != 0)
            throw new InvalidOperationException($"{Path.GetFileName(executable)} terminou com código {process.ExitCode}. {string.Join(Environment.NewLine, errors.TakeLast(8))}");
    }

    public Task<string> CaptureOutputAsync(
        string executable,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken = default) =>
        CaptureAsync(executable, arguments, timeout, includeStandardError: true, cancellationToken);

    public Task<string> CaptureStandardOutputAsync(
        string executable,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken = default) =>
        CaptureAsync(executable, arguments, timeout, includeStandardError: false, cancellationToken);

    private static async Task<string> CaptureAsync(
        string executable, IReadOnlyList<string> arguments, TimeSpan timeout,
        bool includeStandardError, CancellationToken cancellationToken)
    {
        using Process process = CreateProcess(executable, arguments, redirectInput: false, redirectOutput: true);
        if (!process.Start()) throw new InvalidOperationException($"Não foi possível iniciar {Path.GetFileName(executable)}.");
        Task<string> stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> stderr = process.StandardError.ReadToEndAsync(cancellationToken);
        using var timeoutSource = new CancellationTokenSource(timeout);
        using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token);
        try { await process.WaitForExitAsync(linkedSource.Token); }
        catch
        {
            TryKill(process);
            if (timeoutSource.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                throw new TimeoutException($"{Path.GetFileName(executable)} não respondeu dentro do prazo.");
            throw;
        }
        string output = await stdout;
        string errors = await stderr;
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"{Path.GetFileName(executable)} terminou com código {process.ExitCode}. {errors.Trim()}");
        return includeStandardError ? output + Environment.NewLine + errors : output;
    }

    public Process StartWithInput(string executable, IReadOnlyList<string> arguments)
    {
        Process process = CreateProcess(executable, arguments, redirectInput: true);
        if (!process.Start())
        {
            process.Dispose();
            throw new InvalidOperationException($"Não foi possível iniciar {Path.GetFileName(executable)}.");
        }
        return process;
    }

    public static ProcessStartInfo CreateStartInfo(string executable, IReadOnlyList<string> arguments, bool redirectInput)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = redirectInput,
            RedirectStandardError = true,
            RedirectStandardOutput = false
        };
        foreach (string argument in arguments) startInfo.ArgumentList.Add(argument);
        return startInfo;
    }

    private static Process CreateProcess(string executable, IReadOnlyList<string> arguments, bool redirectInput, bool redirectOutput = false)
    {
        ProcessStartInfo startInfo = CreateStartInfo(executable, arguments, redirectInput);
        startInfo.RedirectStandardOutput = redirectOutput;
        return new() { StartInfo = startInfo, EnableRaisingEvents = true };
    }

    public static void TryKill(Process process)
    {
        try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
        catch { }
    }
}
