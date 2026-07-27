using System.Diagnostics;
using System.IO;
using System.Windows.Media.Imaging;

namespace MaskedCode.App.Animations;

/// <summary>
/// Uzun süren maskeleme ve geri açma işlemleri sırasında
/// transparan PNG loader karelerini sürekli oynatır.
/// </summary>
internal sealed class OperationLoaderPlayer
{
    private const int FrameCount = 96;
    private const int FrameRate = 60;

    private IReadOnlyList<BitmapSource>? _cachedFrames;

    /// <summary>
    /// Loader animasyonunu iptal edilene kadar sürekli oynatır.
    /// </summary>
    public async Task PlayAsync(
        string sequenceDirectory,
        Action<BitmapSource> showFrame,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(
            showFrame);

        var frames =
            _cachedFrames ??
            await LoadFramesAsync(
                sequenceDirectory,
                cancellationToken);

        _cachedFrames = frames;

        var frameDuration =
            TimeSpan.FromSeconds(
                1D / FrameRate);

        while (true)
        {
            for (var frameIndex = 0;
                 frameIndex < frames.Count;
                 frameIndex++)
            {
                cancellationToken
                    .ThrowIfCancellationRequested();

                var stopwatch =
                    Stopwatch.StartNew();

                showFrame(
                    frames[frameIndex]);

                var remainingTime =
                    frameDuration -
                    stopwatch.Elapsed;

                if (remainingTime > TimeSpan.Zero)
                {
                    await Task.Delay(
                        remainingTime,
                        cancellationToken);
                }
            }
        }
    }

    private static Task<IReadOnlyList<BitmapSource>>
        LoadFramesAsync(
            string sequenceDirectory,
            CancellationToken cancellationToken)
    {
        return Task.Run<IReadOnlyList<BitmapSource>>(
            () =>
                LoadFrames(
                    sequenceDirectory,
                    cancellationToken),
            cancellationToken);
    }

    private static IReadOnlyList<BitmapSource> LoadFrames(
        string sequenceDirectory,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(sequenceDirectory))
        {
            throw new DirectoryNotFoundException(
                "Loader PNG klasörü bulunamadı: " +
                sequenceDirectory);
        }

        var pngFiles =
            Directory
                .EnumerateFiles(
                    sequenceDirectory,
                    "*.png",
                    SearchOption.TopDirectoryOnly)
                .ToArray();

        if (pngFiles.Length != FrameCount)
        {
            throw new InvalidDataException(
                "Loader tam olarak 96 PNG karesi içermelidir. " +
                $"Bulunan kare sayısı: {pngFiles.Length}.");
        }

        var frames =
            new List<BitmapSource>(
                FrameCount);

        for (var frameNumber = 0;
             frameNumber < FrameCount;
             frameNumber++)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            var fileName =
                $"{frameNumber:0000}.png";

            var framePath =
                Path.Combine(
                    sequenceDirectory,
                    fileName);

            if (!File.Exists(framePath))
            {
                throw new FileNotFoundException(
                    "Loader animasyon karesi bulunamadı: " +
                    fileName,
                    framePath);
            }

            frames.Add(
                LoadBitmap(
                    framePath,
                    fileName));
        }

        return frames;
    }

    private static BitmapSource LoadBitmap(
        string framePath,
        string fileName)
    {
        using var stream =
            new FileStream(
                framePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);

        var bitmap =
            new BitmapImage();

        bitmap.BeginInit();
        bitmap.CacheOption =
            BitmapCacheOption.OnLoad;
        bitmap.CreateOptions =
            BitmapCreateOptions.PreservePixelFormat;
        bitmap.StreamSource =
            stream;
        bitmap.EndInit();
        bitmap.Freeze();

        if (bitmap.PixelWidth != 512 ||
            bitmap.PixelHeight != 512)
        {
            throw new InvalidDataException(
                $"{fileName} loader karesi 512×512 olmalıdır. " +
                $"Bulunan çözünürlük: " +
                $"{bitmap.PixelWidth}×{bitmap.PixelHeight}.");
        }

        return bitmap;
    }
}