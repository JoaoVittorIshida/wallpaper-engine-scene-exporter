using System.Buffers.Binary;
using System.Globalization;
using System.Text.Json;
using System.Windows.Media.Imaging;
using WallpaperSceneExporter.Models;

namespace WallpaperSceneExporter.Services;

public sealed class OutputValidationService
{
    private readonly ProcessRunner _runner;
    private readonly Func<string> _resolveProbe;

    public OutputValidationService(ProcessRunner? runner = null, Func<string>? resolveProbe = null)
    {
        _runner = runner ?? new ProcessRunner();
        _resolveProbe = resolveProbe ?? (() => Path.Combine(AppContext.BaseDirectory, "ffmpeg", "ffprobe.exe"));
    }

    public Task ValidateOutputAsync(string path, CancellationToken cancellationToken = default) =>
        Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".gif" => ValidateGifAsync(path, cancellationToken),
            ".mp4" => ValidateMp4Async(path, cancellationToken),
            _ => throw new InvalidDataException("O formato do arquivo de saída não é suportado.")
        };

    public async Task ValidateGifAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path)) throw new InvalidDataException("O GIF de saída não foi criado.");
        var info = new FileInfo(path);
        if (info.Length < 14) throw new InvalidDataException("O GIF de saída está vazio ou truncado.");

        byte[] header = new byte[6];
        await using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        int read = await stream.ReadAsync(header, cancellationToken);
        string signature = System.Text.Encoding.ASCII.GetString(header, 0, read);
        if (signature is not ("GIF87a" or "GIF89a"))
            throw new InvalidDataException("O arquivo de saída não possui um cabeçalho GIF válido.");

        stream.Position = stream.Length - 1;
        if (stream.ReadByte() != 0x3B)
            throw new InvalidDataException("O GIF de saída está truncado (trailer ausente).");

        stream.Position = 0;
        try
        {
            var decoder = new GifBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            if (decoder.Frames.Count == 0 || decoder.Frames.Any(frame => frame.PixelWidth == 0 || frame.PixelHeight == 0))
                throw new InvalidDataException("O GIF de saída não contém frames legíveis.");
        }
        catch (Exception ex) when (ex is not InvalidDataException)
        {
            throw new InvalidDataException("O GIF de saída não pôde ser decodificado.", ex);
        }
    }

    public Task ValidateMp4Async(string path, CancellationToken cancellationToken = default) =>
        ValidateMp4Async(path, expected: null, cancellationToken);

    public async Task ValidateMp4Async(string path, AppConfig? expected, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path)) throw new InvalidDataException("O MP4 de saída não foi criado.");
        var info = new FileInfo(path);
        if (info.Length < 24) throw new InvalidDataException("O MP4 de saída está vazio ou truncado.");

        await using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        byte[] header = new byte[16];
        bool hasFtyp = false;
        bool hasMediaData = false;
        bool hasMovieMetadata = false;

        while (stream.Position + 8 <= stream.Length)
        {
            long boxStart = stream.Position;
            await stream.ReadExactlyAsync(header.AsMemory(0, 8), cancellationToken);
            ulong boxSize = BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(0, 4));
            string boxType = System.Text.Encoding.ASCII.GetString(header, 4, 4);
            int headerSize = 8;
            if (boxSize == 1)
            {
                await stream.ReadExactlyAsync(header.AsMemory(8, 8), cancellationToken);
                boxSize = BinaryPrimitives.ReadUInt64BigEndian(header.AsSpan(8, 8));
                headerSize = 16;
            }
            else if (boxSize == 0)
            {
                boxSize = checked((ulong)(stream.Length - boxStart));
            }

            if (boxSize < (ulong)headerSize || boxSize > (ulong)(stream.Length - boxStart))
                throw new InvalidDataException("O MP4 de saída contém uma estrutura truncada.");

            hasFtyp |= boxType == "ftyp";
            hasMediaData |= boxType == "mdat";
            hasMovieMetadata |= boxType == "moov";
            stream.Position = checked(boxStart + (long)boxSize);
        }

        if (!hasFtyp || !hasMediaData || !hasMovieMetadata)
            throw new InvalidDataException("O MP4 de saída não contém todos os blocos obrigatórios.");

        string probe = _resolveProbe();
        if (!File.Exists(probe))
            throw new FileNotFoundException("ffprobe.exe é necessário para validar o vídeo antes de finalizar a entrada.", probe);

        string json;
        try
        {
            json = await _runner.CaptureStandardOutputAsync(probe,
                ["-v", "error", "-select_streams", "v:0", "-count_frames", "-show_entries",
                 "stream=codec_name,width,height,nb_read_frames,duration:format=duration", "-of", "json", path],
                TimeSpan.FromMinutes(2), cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            throw new InvalidDataException("O MP4 de saída não contém um vídeo reproduzível.", ex);
        }

        using JsonDocument document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("streams", out JsonElement streams) || streams.GetArrayLength() == 0)
            throw new InvalidDataException("O MP4 de saída não contém uma trilha de vídeo.");
        JsonElement video = streams[0];
        if (!video.TryGetProperty("codec_name", out JsonElement codec) || codec.GetString() != "h264" ||
            !video.TryGetProperty("width", out JsonElement width) || width.GetInt32() <= 0 ||
            !video.TryGetProperty("height", out JsonElement height) || height.GetInt32() <= 0 ||
            !video.TryGetProperty("nb_read_frames", out JsonElement frames) ||
            !long.TryParse(frames.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long frameCount) || frameCount <= 0)
            throw new InvalidDataException("O MP4 de saída não contém frames H.264 válidos.");

        double duration = video.TryGetProperty("duration", out JsonElement streamDuration) &&
            double.TryParse(streamDuration.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed)
                ? parsed : 0;
        if (duration <= 0 && document.RootElement.TryGetProperty("format", out JsonElement format) &&
            format.TryGetProperty("duration", out JsonElement formatDuration))
            double.TryParse(formatDuration.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out duration);
        if (!double.IsFinite(duration) || duration <= 0)
            throw new InvalidDataException("O MP4 de saída não possui duração válida.");

        if (expected is not null && (width.GetInt32() != expected.Width || height.GetInt32() != expected.Height ||
            frameCount != (long)expected.DurationSeconds * expected.CaptureFps ||
            Math.Abs(duration - expected.DurationSeconds) > 1d / expected.CaptureFps + 0.05))
            throw new InvalidDataException("O MP4 de saída não corresponde à resolução, duração ou quantidade de frames solicitada.");
    }
}
