using WallpaperSceneExporter.Services;

namespace WallpaperSceneExporter.Tests;

internal static class TestMedia
{
    public static byte[] Gif() => Convert.FromBase64String("R0lGODlhAQABAIAAAAAAAP///ywAAAAAAQABAAACAUwAOw==");

    public static async Task CreateMp4Async(string path, CancellationToken cancellationToken)
    {
        string ffmpeg = Path.Combine(AppContext.BaseDirectory, "ffmpeg", "ffmpeg.exe");
        await new ProcessRunner().RunAsync(ffmpeg,
            ["-hide_banner", "-loglevel", "error", "-y", "-f", "lavfi", "-i", "testsrc2=size=16x16:rate=2",
             "-frames:v", "2", "-c:v", "libx264", "-pix_fmt", "yuv420p", "-movflags", "+faststart", path],
            timeout: TimeSpan.FromSeconds(20), cancellationToken: cancellationToken);
    }
}
