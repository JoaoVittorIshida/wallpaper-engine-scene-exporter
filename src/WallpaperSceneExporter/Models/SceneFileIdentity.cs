using System.Security.Cryptography;

namespace WallpaperSceneExporter.Models;

public sealed record SceneFileIdentity(long Length, string Sha256)
{
    public static async Task<SceneFileIdentity> CreateAsync(string path, CancellationToken cancellationToken = default)
    {
        await using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        byte[] hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return new(stream.Length, Convert.ToHexString(hash));
    }
}
