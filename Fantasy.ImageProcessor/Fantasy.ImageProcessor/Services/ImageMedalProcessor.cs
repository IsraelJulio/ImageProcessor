
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.IO.Compression;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Webp;

namespace Fantasy.ImageProcessor.Services;

public sealed class ImageMedalProcessor
{
    private const byte TransparentAlpha = 0;
    private const byte OpaqueAlpha = 255;
    public async Task<byte[]> NormalizeTo512Async(IFormFile file)
    {
        using var inputStream = file.OpenReadStream();
        using var image = await Image.LoadAsync<Rgba32>(inputStream);

        image.Mutate(x =>
            x.Resize(new ResizeOptions
            {
                Size = new Size(512, 512),
                Mode = ResizeMode.Max
            }));

        using var canvas = new Image<Rgba32>(512, 512, Color.Transparent);

        var xPos = (512 - image.Width) / 2;
        var yPos = (512 - image.Height) / 2;

        canvas.Mutate(ctx => ctx.DrawImage(image, new Point(xPos, yPos), 1f));

        using var output = new MemoryStream();
        await canvas.SaveAsPngAsync(output);

        return output.ToArray();
    }

    public async Task<byte[]> NormalizeManyToZipAsync(List<IFormFile> files)
    {
        using var zipStream = new MemoryStream();

        using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var file in files)
            {
                Console.WriteLine($"Processando: {file.FileName}");

                await using var inputStream = file.OpenReadStream();
                var normalizedBytes = await ProcessAsync(inputStream, 512);

                Console.WriteLine($"Finalizado: {file.FileName}");

                var originalName = Path.GetFileNameWithoutExtension(file.FileName);
                var entry = archive.CreateEntry($"{originalName}-512.png");

                await using var entryStream = entry.Open();
                await entryStream.WriteAsync(normalizedBytes);
            }
        }

        return zipStream.ToArray();
    }

    public async Task<byte[]> ProcessAsync(Stream input, int finalSize = 512, CancellationToken cancellationToken = default)
    {
        using var image = await Image.LoadAsync<Rgba32>(input, cancellationToken);

        RemoveCheckerboardBackgroundConnectedToEdges(image);
        TrimTransparentEdges(image);

        using var normalized = CreateSquareCanvas(image, finalSize);

        await using var output = new MemoryStream();
        await normalized.SaveAsPngAsync(output, cancellationToken);
        return output.ToArray();
    }

    private static void RemoveCheckerboardBackgroundConnectedToEdges(Image<Rgba32> image)
    {
        var width = image.Width;
        var height = image.Height;
        var visited = new bool[width, height];
        var queue = new Queue<Point>();

        void TryEnqueue(int x, int y)
        {
            if (x < 0 || y < 0 || x >= width || y >= height)
                return;

            if (visited[x, y])
                return;

            var pixel = image[x, y];

            if (!LooksLikeCheckerboardBackground(pixel))
                return;

            visited[x, y] = true;
            queue.Enqueue(new Point(x, y));
        }

        for (var x = 0; x < width; x++)
        {
            TryEnqueue(x, 0);
            TryEnqueue(x, height - 1);
        }

        for (var y = 0; y < height; y++)
        {
            TryEnqueue(0, y);
            TryEnqueue(width - 1, y);
        }

        while (queue.Count > 0)
        {
            var p = queue.Dequeue();
            var pixel = image[p.X, p.Y];
            pixel.A = TransparentAlpha;
            image[p.X, p.Y] = pixel;

            TryEnqueue(p.X + 1, p.Y);
            TryEnqueue(p.X - 1, p.Y);
            TryEnqueue(p.X, p.Y + 1);
            TryEnqueue(p.X, p.Y - 1);
        }

        SoftCleanEdges(image);
    }

    private static bool LooksLikeCheckerboardBackground(Rgba32 pixel)
    {
        if (pixel.A < 30)
            return true;

        var max = Math.Max(pixel.R, Math.Max(pixel.G, pixel.B));
        var min = Math.Min(pixel.R, Math.Min(pixel.G, pixel.B));
        var isAlmostGray = max - min <= 12;
        var isVeryLight = pixel.R >= 220 && pixel.G >= 220 && pixel.B >= 220;

        return isAlmostGray && isVeryLight;
    }

    private static void SoftCleanEdges(Image<Rgba32> image)
    {
        var width = image.Width;
        var height = image.Height;
        var toTransparent = new List<Point>();

        for (var y = 1; y < height - 1; y++)
        {
            for (var x = 1; x < width - 1; x++)
            {
                var pixel = image[x, y];
                if (pixel.A == TransparentAlpha)
                    continue;

                if (!LooksLikeCheckerboardBackground(pixel))
                    continue;

                var transparentNeighbors = 0;
                if (image[x + 1, y].A == TransparentAlpha) transparentNeighbors++;
                if (image[x - 1, y].A == TransparentAlpha) transparentNeighbors++;
                if (image[x, y + 1].A == TransparentAlpha) transparentNeighbors++;
                if (image[x, y - 1].A == TransparentAlpha) transparentNeighbors++;

                if (transparentNeighbors >= 2)
                    toTransparent.Add(new Point(x, y));
            }
        }

        foreach (var point in toTransparent)
        {
            var pixel = image[point.X, point.Y];
            pixel.A = TransparentAlpha;
            image[point.X, point.Y] = pixel;
        }
    }

    private static void TrimTransparentEdges(Image<Rgba32> image)
    {
        var minX = image.Width;
        var minY = image.Height;
        var maxX = 0;
        var maxY = 0;
        var found = false;

        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                if (image[x, y].A <= 10)
                    continue;

                found = true;
                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
            }
        }

        if (!found)
            return;

        var padding = 12;
        minX = Math.Max(0, minX - padding);
        minY = Math.Max(0, minY - padding);
        maxX = Math.Min(image.Width - 1, maxX + padding);
        maxY = Math.Min(image.Height - 1, maxY + padding);

        var cropWidth = maxX - minX + 1;
        var cropHeight = maxY - minY + 1;

        image.Mutate(x => x.Crop(new Rectangle(minX, minY, cropWidth, cropHeight)));
    }

    private static Image<Rgba32> CreateSquareCanvas(Image<Rgba32> source, int finalSize)
    {
        var maxContentSize = (int)(finalSize * 0.92);

        source.Mutate(x => x.Resize(new ResizeOptions
        {
            Size = new Size(maxContentSize, maxContentSize),
            Mode = ResizeMode.Max,
            Sampler = KnownResamplers.Lanczos3
        }));

        var canvas = new Image<Rgba32>(finalSize, finalSize, Color.Transparent);
        var posX = (finalSize - source.Width) / 2;
        var posY = (finalSize - source.Height) / 2;

        canvas.Mutate(x => x.DrawImage(source, new Point(posX, posY), OpaqueAlpha / 255f));
        return canvas;
    }
    public async Task<byte[]> CompressManyToZipAsync(
    List<IFormFile> files,
    int maxWidth = 1024,
    int quality = 75,
    string format = "webp",
    CancellationToken cancellationToken = default)
    {
        using var zipStream = new MemoryStream();

        using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var file in files)
            {
                Console.WriteLine($"Comprimindo: {file.FileName}");

                await using var inputStream = file.OpenReadStream();

                var compressedBytes = await CompressImageAsync(
                    inputStream,
                    maxWidth,
                    quality,
                    format,
                    cancellationToken);

                var originalName = Path.GetFileNameWithoutExtension(file.FileName);
                var extension = format.ToLowerInvariant() == "jpg" || format.ToLowerInvariant() == "jpeg"
                    ? "jpg"
                    : "webp";

                var entry = archive.CreateEntry($"{originalName}-compressed.{extension}");

                await using var entryStream = entry.Open();
                await entryStream.WriteAsync(compressedBytes, cancellationToken);

                Console.WriteLine($"Finalizado: {file.FileName}");
            }
        }

        return zipStream.ToArray();
    }

    public async Task<byte[]> CompressImageAsync(
        Stream input,
        int maxWidth = 1024,
        int quality = 75,
        string format = "webp",
        CancellationToken cancellationToken = default)
    {
        using var image = await Image.LoadAsync(input, cancellationToken);

        if (image.Width > maxWidth)
        {
            var ratio = (double)maxWidth / image.Width;
            var newHeight = (int)(image.Height * ratio);

            image.Mutate(x => x.Resize(maxWidth, newHeight));
        }

        await using var output = new MemoryStream();

        format = format.ToLowerInvariant();

        if (format == "jpg" || format == "jpeg")
        {
            await image.SaveAsJpegAsync(output, new JpegEncoder
            {
                Quality = quality
            }, cancellationToken);
        }
        else
        {
            await image.SaveAsWebpAsync(output, new WebpEncoder
            {
                Quality = quality
            }, cancellationToken);
        }

        return output.ToArray();
    }
}
