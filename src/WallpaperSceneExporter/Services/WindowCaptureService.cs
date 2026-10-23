using System.Diagnostics;
using System.Runtime.InteropServices;
using Vortice.Direct3D11;
using Vortice.DXGI;
using WallpaperSceneExporter.Helpers;
using WallpaperSceneExporter.Interop;
using WallpaperSceneExporter.Models;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;

namespace WallpaperSceneExporter.Services;

public sealed class WindowCaptureService : IWindowCaptureService
{
    private readonly FfmpegService _ffmpeg;
    private readonly LoggingService _log;

    public WindowCaptureService(FfmpegService ffmpeg, LoggingService log)
    {
        _ffmpeg = ffmpeg;
        _log = log;
    }

    public async Task CaptureAsync(nint hwnd, AppConfig config, string outputMp4, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        if (!GraphicsCaptureSession.IsSupported())
            throw new PlatformNotSupportedException("Windows Graphics Capture não é suportado neste computador.");
        if (!NativeWindow.Exists(hwnd)) throw new InvalidOperationException("A janela de renderização foi fechada antes da captura.");

        using var direct3D = new Direct3D11Interop();
        GraphicsCaptureItem item = GraphicsCaptureInterop.CreateForWindow(hwnd);
        int sourceWidth = item.Size.Width;
        int sourceHeight = item.Size.Height;
        if (sourceWidth <= 0 || sourceHeight <= 0 || sourceWidth > 8192 || sourceHeight > 8192)
            throw new InvalidOperationException($"A superfície capturável tem uma dimensão inválida: {sourceWidth}x{sourceHeight}.");
        await _log.InfoAsync(
            sourceWidth == config.Width && sourceHeight == config.Height
                ? $"Superfície de captura pronta em {sourceWidth}x{sourceHeight}."
                : $"Superfície de captura em {sourceWidth}x{sourceHeight}; redimensionando para {config.Width}x{config.Height}.",
            cancellationToken);

        using Direct3D11CaptureFramePool framePool = Direct3D11CaptureFramePool.CreateFreeThreaded(
            direct3D.WinRtDevice, DirectXPixelFormat.B8G8R8A8UIntNormalized, 2, item.Size);
        using GraphicsCaptureSession session = framePool.CreateCaptureSession(item);
        session.IsCursorCaptureEnabled = false;

        int byteCount = checked(sourceWidth * sourceHeight * 4);
        byte[] latest = new byte[byteCount];
        byte[] scratch = new byte[byteCount];
        byte[] outgoing = new byte[byteCount];
        object frameLock = new();
        object captureGate = new();
        bool hasFrame = false;
        long latestSequence = 0;
        var firstFrame = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var visibleFrame = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var captureError = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        ID3D11Texture2D? staging = null;

        void OnFrameArrived(Direct3D11CaptureFramePool sender, object args)
        {
            lock (captureGate)
            {
                try
                {
                    using Direct3D11CaptureFrame? frame = sender.TryGetNextFrame();
                    if (frame is null) return;
                    CaptureSurfaceGuard.Validate(sourceWidth, sourceHeight, frame.ContentSize.Width, frame.ContentSize.Height);
                    using ID3D11Texture2D source = direct3D.GetTexture(frame.Surface);
                    staging ??= direct3D.Device.CreateTexture2D(new Texture2DDescription(
                        Format.B8G8R8A8_UNorm,
                        (uint)sourceWidth,
                        (uint)sourceHeight,
                        1,
                        1,
                        BindFlags.None,
                        ResourceUsage.Staging,
                        CpuAccessFlags.Read));
                    direct3D.Context.CopyResource(staging, source);
                    MappedSubresource mapped = direct3D.Context.Map(staging, 0, MapMode.Read, Vortice.Direct3D11.MapFlags.None);
                    try
                    {
                        int rowBytes = sourceWidth * 4;
                        for (int y = 0; y < sourceHeight; y++)
                            Marshal.Copy(IntPtr.Add(mapped.DataPointer, y * checked((int)mapped.RowPitch)), scratch, y * rowBytes, rowBytes);
                    }
                    finally { direct3D.Context.Unmap(staging, 0); }

                    lock (frameLock)
                    {
                        (latest, scratch) = (scratch, latest);
                        hasFrame = true;
                        latestSequence++;
                    }
                    firstFrame.TrySetResult();
                    if (ContainsVisiblePixels(latest)) visibleFrame.TrySetResult();
                }
                catch (Exception ex)
                {
                    captureError.TrySetResult(ex);
                    firstFrame.TrySetException(ex);
                    visibleFrame.TrySetException(ex);
                }
            }
        }

        framePool.FrameArrived += OnFrameArrived;
        var errors = new List<string>();
        Process? encoder = null;

        try
        {
            session.StartCapture();
            await firstFrame.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
            await _log.InfoAsync("Aguardando a Scene exibir o primeiro frame visível…", cancellationToken);
            try
            {
                await visibleFrame.Task.WaitAsync(TimeSpan.FromSeconds(20), cancellationToken);
            }
            catch (TimeoutException)
            {
                throw new InvalidDataException("A Scene permaneceu preta por 20 segundos. Verifique se o pacote é válido e tente novamente.");
            }

            encoder = _ffmpeg.StartRawEncoder(config, outputMp4, sourceWidth, sourceHeight);
            encoder.ErrorDataReceived += (_, e) => { if (e.Data is not null) lock (errors) errors.Add(e.Data); };
            encoder.BeginErrorReadLine();
            int totalFrames = checked(config.DurationSeconds * config.CaptureFps);
            var clock = Stopwatch.StartNew();
            long consumedSequence = 0;
            int duplicatedFrames = 0;
            long droppedSourceFrames = 0;

            for (int index = 0; index < totalFrames; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (captureError.Task.IsCompleted) throw await captureError.Task;
                if (!NativeWindow.Exists(hwnd)) throw new InvalidOperationException("A janela do Wallpaper Engine foi fechada durante a gravação.");

                TimeSpan target = TimeSpan.FromSeconds((double)index / config.CaptureFps);
                TimeSpan wait = target - clock.Elapsed;
                if (wait > TimeSpan.Zero) await Task.Delay(wait, cancellationToken);

                lock (frameLock)
                {
                    if (!hasFrame) throw new InvalidOperationException("Nenhum frame válido foi recebido.");
                    if (latestSequence == consumedSequence) duplicatedFrames++;
                    else if (consumedSequence > 0) droppedSourceFrames += Math.Max(0, latestSequence - consumedSequence - 1);
                    consumedSequence = latestSequence;
                    System.Buffer.BlockCopy(latest, 0, outgoing, 0, byteCount);
                }
                await encoder.StandardInput.BaseStream.WriteAsync(outgoing, cancellationToken);
                progress?.Report((index + 1d) / totalFrames);
            }

            if (captureError.Task.IsCompleted) throw await captureError.Task;

            await encoder.StandardInput.BaseStream.FlushAsync(cancellationToken);
            encoder.StandardInput.Close();
            await encoder.WaitForExitAsync(cancellationToken);
            if (encoder.ExitCode != 0)
                throw new InvalidOperationException($"FFmpeg falhou ao criar o MP4 (código {encoder.ExitCode}). {string.Join(Environment.NewLine, errors.TakeLast(8))}");
            if (!File.Exists(outputMp4) || new FileInfo(outputMp4).Length == 0)
                throw new InvalidDataException("O MP4 intermediário não foi criado corretamente.");
            await _log.InfoAsync($"Ritmo da captura: {totalFrames} frames gravados, {duplicatedFrames} repetidos, {droppedSourceFrames} frames de origem substituídos.", cancellationToken);
        }
        catch
        {
            if (encoder is not null) ProcessRunner.TryKill(encoder);
            throw;
        }
        finally
        {
            encoder?.Dispose();
            framePool.FrameArrived -= OnFrameArrived;
            lock (captureGate) staging?.Dispose();
        }

        await _log.InfoAsync("Captura concluída", cancellationToken);
    }

    private static bool ContainsVisiblePixels(byte[] pixels)
    {
        // Amostragem distribuída: evita aceitar uma superfície totalmente preta sem
        // impor custo relevante a cada frame recebido pela GPU.
        const int pixelStride = 64;
        int byteStride = pixelStride * 4;
        for (int index = 0; index + 2 < pixels.Length; index += byteStride)
        {
            if (pixels[index] > 8 || pixels[index + 1] > 8 || pixels[index + 2] > 8)
                return true;
        }
        return false;
    }
}
