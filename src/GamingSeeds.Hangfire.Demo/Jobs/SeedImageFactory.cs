using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace GamingSeeds.Hangfire.Demo.Jobs;

public static class SeedImageFactory
{
    public static string SourcePath(string root, int imageId) => Path.Combine(root, $"seed-{imageId}-source.png");
    public static string ProcessedPath(string root, int imageId) => Path.Combine(root, $"seed-{imageId}-processed.png");
    public static string ThumbnailPath(string root, int imageId) => Path.Combine(root, $"seed-{imageId}-thumb.png");
    public static string OptimizedPath(string root, int imageId) => Path.Combine(root, $"seed-{imageId}-optimized.jpg");

    // A procedurally-generated "map" so the upload has something real to process. Deterministic per imageId.
    public static async Task CreateSource(string path, int imageId)
    {
        const int width = 960, height = 640, cell = 40;
        var random = new Random(imageId);
        var palette = new[]
        {
            new Rgba32(46, 105, 76), new Rgba32(83, 143, 94), new Rgba32(122, 168, 92),
            new Rgba32(64, 121, 140), new Rgba32(196, 176, 108), new Rgba32(120, 96, 72)
        };

        using var image = new Image<Rgba32>(width, height);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var colour = palette[random.Next(palette.Length)];
                // Sample one colour per cell so we get chunky "terrain" blocks, not noise.
                if (x % cell == 0 && y % cell == 0)
                {
                    image[x, y] = colour;
                }
            }
        }

        for (var by = 0; by < height; by += cell)
        {
            for (var bx = 0; bx < width; bx += cell)
            {
                var colour = image[bx, by];
                for (var y = by; y < Math.Min(by + cell, height); y++)
                    for (var x = bx; x < Math.Min(bx + cell, width); x++)
                        image[x, y] = colour;
            }
        }

        await image.SaveAsync(path);
    }
}
