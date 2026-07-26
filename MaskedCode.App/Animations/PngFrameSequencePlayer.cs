using System.Diagnostics;
using System.IO;
using System.Windows.Media.Imaging;

namespace MaskedCode.App.Animations;

internal sealed class PngFrameSequencePlayer
{
    private const int ExpectedFrameCount = 60;
    private const int MinimumFrameRate = 1;
    private const int MaximumFrameRate = 240;

    public async Task PlayAsync(
        string sequenceDirectory,
        int frameRate,
        Action<BitmapSource> showFrame,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(showFrame);

        ValidateFrameRate(frameRate);

        var frames = await LoadFramesAsync(
            sequenceDirectory,
            cancellationToken);

        var stopwatch = Stopwatch.StartNew();

        for (var frameIndex = 0;
             frameIndex < frames.Count;
             frameIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var targetElapsed = TimeSpan.FromSeconds(
                (double)frameIndex / frameRate);

            var remainingTime =
                targetElapsed - stopwatch.Elapsed;

            if (remainingTime > TimeSpan.Zero)
            {
                await Task.Delay(
                    remainingTime,
                    cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();

            showFrame(frames[frameIndex]);
        }
    }

    internal static Task<IReadOnlyList<BitmapSource>> LoadFramesAsync(
        string sequenceDirectory,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sequenceDirectory))
        {
            throw new ArgumentException(
                "PNG kare dizisi klasörü belirtilmelidir.",
                nameof(sequenceDirectory));
        }

        return Task.Run<IReadOnlyList<BitmapSource>>(
            () => LoadFrames(
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
                $"PNG kare dizisi klasörü bulunamadı: " +
                $"{sequenceDirectory}");
        }

        ValidateDirectoryContents(sequenceDirectory);

        var frames = new List<BitmapSource>(
            ExpectedFrameCount);

        for (var frameNumber = 1;
             frameNumber <= ExpectedFrameCount;
             frameNumber++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var fileName =
                $"frame-{frameNumber:000000}.png";

            var framePath = Path.Combine(
                sequenceDirectory,
                fileName);

            if (!File.Exists(framePath))
            {
                throw new FileNotFoundException(
                    $"PNG animasyon karesi bulunamadı: {fileName}",
                    framePath);
            }

            var fileInfo = new FileInfo(framePath);

            if (fileInfo.Length == 0)
            {
                throw new InvalidDataException(
                    $"PNG animasyon karesi boş: {fileName}");
            }

            frames.Add(
                LoadBitmap(framePath, fileName));
        }

        return frames;
    }

    private static void ValidateDirectoryContents(
        string sequenceDirectory)
    {
        var pngFiles = Directory
            .EnumerateFiles(
                sequenceDirectory,
                "*.png",
                SearchOption.TopDirectoryOnly)
            .ToArray();

        if (pngFiles.Length != ExpectedFrameCount)
        {
            throw new InvalidDataException(
                $"PNG kare dizisi tam olarak " +
                $"{ExpectedFrameCount} kare içermelidir. " +
                $"Bulunan kare sayısı: {pngFiles.Length}.");
        }
    }

    private static BitmapSource LoadBitmap(string framePath, string fileName)
    {
        try
        {
            using var stream = new FileStream(
                framePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);

            var bitmap = new BitmapImage();

            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();

            if (bitmap.PixelWidth != 512 ||
                bitmap.PixelHeight != 512)
            {
                throw new InvalidDataException(
                    $"PNG animasyon karesi 512×512 olmalıdır: " +
                    $"{fileName}. Bulunan çözünürlük: " +
                    $"{bitmap.PixelWidth}×{bitmap.PixelHeight}.");
            }

            return bitmap;
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new InvalidDataException(
                $"PNG animasyon karesi açılamadı veya bozuk: " +
                $"{fileName}",
                exception);
        }
    }

    private static void ValidateFrameRate(int frameRate)
    {
        if (frameRate is < MinimumFrameRate
            or > MaximumFrameRate)
        {
            throw new ArgumentOutOfRangeException(
                nameof(frameRate),
                frameRate,
                $"Kare hızı {MinimumFrameRate} ile " +
                $"{MaximumFrameRate} arasında olmalıdır.");
        }
    }
}