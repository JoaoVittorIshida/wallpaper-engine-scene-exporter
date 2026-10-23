using WallpaperSceneExporter.Helpers;
using WallpaperSceneExporter.Services;

namespace WallpaperSceneExporter.Tests;

public sealed class OutputAndDeletionTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "wse-tests-" + Guid.NewGuid().ToString("N"));

    public OutputAndDeletionTests() => Directory.CreateDirectory(_directory);

    [Fact]
    public async Task ConversionSuccessDeletesInput()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string input = Path.Combine(_directory, "scene.pkg");
        string output = Path.Combine(_directory, "valid.gif");
        await File.WriteAllTextAsync(input, "scene", cancellationToken);
        await File.WriteAllBytesAsync(output, ValidGif(), cancellationToken);
        var finalizer = new SceneFinalizer(new OutputValidationService());

        bool deleted = await finalizer.DeleteInputAfterSuccessAsync(input, output, cancellationToken);

        Assert.True(deleted);
        Assert.False(File.Exists(input));
    }

    [Fact]
    public async Task ConversionFailurePreservesInput()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string input = Path.Combine(_directory, "scene.pkg");
        await File.WriteAllTextAsync(input, "scene", cancellationToken);
        var finalizer = new SceneFinalizer(new OutputValidationService());

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            finalizer.DeleteInputAfterSuccessAsync(input, Path.Combine(_directory, "missing.gif"), cancellationToken));

        Assert.True(File.Exists(input));
    }

    [Fact]
    public async Task ValidMp4DeletesInputAfterValidation()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string input = Path.Combine(_directory, "scene.pkg");
        string output = Path.Combine(_directory, "valid.mp4");
        await File.WriteAllTextAsync(input, "scene", cancellationToken);
        await TestMedia.CreateMp4Async(output, cancellationToken);
        var finalizer = new SceneFinalizer(new OutputValidationService());

        bool deleted = await finalizer.DeleteInputAfterSuccessAsync(input, output, cancellationToken);

        Assert.True(deleted);
        Assert.False(File.Exists(input));
    }

    [Fact]
    public async Task TruncatedMp4PreservesInput()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string input = Path.Combine(_directory, "scene.pkg");
        string output = Path.Combine(_directory, "truncated.mp4");
        await File.WriteAllTextAsync(input, "scene", cancellationToken);
        await File.WriteAllBytesAsync(output, ValidMp4()[..^4], cancellationToken);
        var finalizer = new SceneFinalizer(new OutputValidationService());

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            finalizer.DeleteInputAfterSuccessAsync(input, output, cancellationToken));

        Assert.True(File.Exists(input));
    }

    [Fact]
    public async Task EmptyMp4BoxesCannotAuthorizeInputDeletion()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string input = Path.Combine(_directory, "scene.pkg");
        string output = Path.Combine(_directory, "empty-boxes.mp4");
        await File.WriteAllTextAsync(input, "preserve me", cancellationToken);
        await File.WriteAllBytesAsync(output, ValidMp4(), cancellationToken);

        await Assert.ThrowsAsync<InvalidDataException>(() => new SceneFinalizer(new OutputValidationService())
            .DeleteInputAfterSuccessAsync(input, output, cancellationToken));

        Assert.Equal("preserve me", await File.ReadAllTextAsync(input, cancellationToken));
    }

    [Fact]
    public async Task Mp4MustMatchRequestedDimensionsAndFrameCount()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string output = Path.Combine(_directory, "real.mp4");
        await TestMedia.CreateMp4Async(output, cancellationToken);
        var expected = new WallpaperSceneExporter.Models.AppConfig
        {
            Width = 16, Height = 16, DurationSeconds = 1, CaptureFps = 2
        };
        var validator = new OutputValidationService();
        await validator.ValidateMp4Async(output, expected, cancellationToken);
        expected.DurationSeconds = 2;
        await Assert.ThrowsAsync<InvalidDataException>(() => validator.ValidateMp4Async(output, expected, cancellationToken));
    }

    [Fact]
    public async Task ClaimedInputIsRestoredAfterFailure()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var paths = new AppPaths(_directory);
        paths.EnsureDirectories();
        await File.WriteAllTextAsync(paths.InputSceneFile, "original scene", cancellationToken);
        var service = new SceneWorkItemService(paths);

        var item = service.Claim(paths.InputSceneFile);
        Assert.False(File.Exists(paths.InputSceneFile));
        await service.RestoreAsync(item, cancellationToken);

        Assert.Equal("original scene", await File.ReadAllTextAsync(paths.InputSceneFile, cancellationToken));
    }

    [Fact]
    public async Task RejectsAFileWithoutGifSignature()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string output = Path.Combine(_directory, "invalid.gif");
        await File.WriteAllTextAsync(output, "not really a gif file", cancellationToken);
        await Assert.ThrowsAsync<InvalidDataException>(() => new OutputValidationService().ValidateGifAsync(output, cancellationToken));
    }

    [Fact]
    public async Task RejectsTruncatedGifWithAValidHeader()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string output = Path.Combine(_directory, "truncated.gif");
        await File.WriteAllBytesAsync(output, ValidGif()[..^1], cancellationToken);

        await Assert.ThrowsAsync<InvalidDataException>(() => new OutputValidationService().ValidateGifAsync(output, cancellationToken));
    }

    private static byte[] ValidGif() => Convert.FromBase64String("R0lGODlhAQABAIAAAAAAAP///ywAAAAAAQABAAACAUwAOw==");

    private static byte[] ValidMp4() =>
    [
        0, 0, 0, 12, (byte)'f', (byte)'t', (byte)'y', (byte)'p', (byte)'i', (byte)'s', (byte)'o', (byte)'m',
        0, 0, 0, 8, (byte)'m', (byte)'d', (byte)'a', (byte)'t',
        0, 0, 0, 8, (byte)'m', (byte)'o', (byte)'o', (byte)'v'
    ];

    public void Dispose() => Directory.Delete(_directory, true);
}
