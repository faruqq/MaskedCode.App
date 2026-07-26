using MaskedCode.App.Animations;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MaskedCode.App.Tests.Animations;

public sealed class PngFrameSequencePlayerTests
{
    [Fact]
    public async Task LoadFramesAsyncWithValidSequenceReturnsAllFrames()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            CreateValidFrameSequence(directory);

            var frames =
                await PngFrameSequencePlayer.LoadFramesAsync(
                    directory,
                    CancellationToken.None);

            Assert.Equal(60, frames.Count);
            Assert.All(
                frames,
                frame =>
                {
                    Assert.Equal(512, frame.PixelWidth);
                    Assert.Equal(512, frame.PixelHeight);
                    Assert.True(frame.IsFrozen);
                });
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task LoadFramesAsyncWithMissingFrameThrowsException()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            CreateValidFrameSequence(directory);

            File.Delete(
                Path.Combine(
                    directory,
                    "frame-000025.png"));

            var exception = await Assert.ThrowsAsync<
                InvalidDataException>(
                () => PngFrameSequencePlayer.LoadFramesAsync(
                    directory,
                    CancellationToken.None));

            Assert.Contains(
                "60 kare",
                exception.Message);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task LoadFramesAsyncWithEmptyFrameThrowsException()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            CreateValidFrameSequence(directory);

            var emptyFramePath = Path.Combine(
                directory,
                "frame-000018.png");

            File.WriteAllBytes(
                emptyFramePath,
                []);

            var exception = await Assert.ThrowsAsync<
                InvalidDataException>(
                () => PngFrameSequencePlayer.LoadFramesAsync(
                    directory,
                    CancellationToken.None));

            Assert.Contains(
                "boş",
                exception.Message);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task LoadFramesAsyncWithCorruptedFrameThrowsException()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            CreateValidFrameSequence(directory);

            var corruptedFramePath = Path.Combine(
                directory,
                "frame-000041.png");

            await File.WriteAllTextAsync(
                corruptedFramePath,
                "geçerli bir PNG değildir");

            var exception = await Assert.ThrowsAsync<
                InvalidDataException>(
                () => PngFrameSequencePlayer.LoadFramesAsync(
                    directory,
                    CancellationToken.None));

            Assert.Contains(
                "bozuk",
                exception.Message);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task PlayAsyncWithCancellationStopsPlayback()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            CreateValidFrameSequence(directory);

            var player = new PngFrameSequencePlayer();
            using var cancellationTokenSource =
                new CancellationTokenSource();

            var shownFrameCount = 0;

            await Assert.ThrowsAnyAsync<
                OperationCanceledException>(
                () => player.PlayAsync(
                    directory,
                    frameRate: 60,
                    _ =>
                    {
                        shownFrameCount++;

                        if (shownFrameCount == 3)
                        {
                            cancellationTokenSource.Cancel();
                        }
                    },
                    cancellationTokenSource.Token));

            Assert.Equal(3, shownFrameCount);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "MaskedCode.Tests",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(directory);

        return directory;
    }

    private static void CreateValidFrameSequence(
        string directory)
    {
        var frameBytes = CreatePngBytes();

        for (var frameNumber = 1;
             frameNumber <= 60;
             frameNumber++)
        {
            var framePath = Path.Combine(
                directory,
                $"frame-{frameNumber:000000}.png");

            File.WriteAllBytes(
                framePath,
                frameBytes);
        }
    }

    private static byte[] CreatePngBytes()
    {
        var pixels =
            new byte[512 * 512 * 4];

        var bitmap = BitmapSource.Create(
            pixelWidth: 512,
            pixelHeight: 512,
            dpiX: 96,
            dpiY: 96,
            PixelFormats.Bgra32,
            palette: null,
            pixels,
            stride: 512 * 4);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(
            BitmapFrame.Create(bitmap));

        using var stream = new MemoryStream();
        encoder.Save(stream);

        return stream.ToArray();
    }
}