namespace WallpaperSceneExporter.Services;

public sealed class FileStabilityService
{
    public async Task WaitUntilReadyAsync(
        string path,
        TimeSpan? pollInterval = null,
        int stableObservations = 3,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        TimeSpan interval = pollInterval ?? TimeSpan.FromMilliseconds(500);
        DateTimeOffset deadline = DateTimeOffset.UtcNow + (timeout ?? TimeSpan.FromMinutes(5));
        long previousLength = -1;
        DateTime previousWrite = DateTime.MinValue;
        int stable = 0;

        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!File.Exists(path))
                throw new FileNotFoundException("O arquivo scene.pkg não existe mais.", path);

            try
            {
                var info = new FileInfo(path);
                if (info.Length > 0 && info.Length == previousLength && info.LastWriteTimeUtc == previousWrite)
                {
                    using FileStream _ = new(path, FileMode.Open, FileAccess.Read, FileShare.None);
                    if (++stable >= stableObservations) return;
                }
                else
                {
                    stable = 0;
                    previousLength = info.Length;
                    previousWrite = info.LastWriteTimeUtc;
                }
            }
            catch (IOException)
            {
                stable = 0;
            }
            catch (UnauthorizedAccessException)
            {
                stable = 0;
            }

            await Task.Delay(interval, cancellationToken);
        }

        throw new TimeoutException("scene.pkg permaneceu bloqueado ou instável por tempo demais.");
    }
}
