using GamingSeeds.Hangfire.Demo.Data;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace GamingSeeds.Hangfire.Demo.Jobs;

public class ImageProcessingJobs(
    IWebHostEnvironment env,
    IOptions<DemoOptions> options,
    ILogger<ImageProcessingJobs> logger) : IImageProcessingJobs
{
    private string GeneratedRoot => Path.Combine(env.WebRootPath, "images", "generated");

    public async Task ProcessImage(int imageId)
    {
        var source = SeedImageFactory.SourcePath(GeneratedRoot, imageId);

        using var image = await Image.LoadAsync(source);
        image.Mutate(x => x.Resize(new ResizeOptions
        {
            Size = new Size(1280, 720),
            Mode = ResizeMode.Crop
        }));

        await SimulateWork();
        await image.SaveAsync(SeedImageFactory.ProcessedPath(GeneratedRoot, imageId));
        logger.LogInformation("Processed image {ImageId}", imageId);
    }

    public async Task GenerateThumbnails(int imageId)
    {
        using var image = await Image.LoadAsync(SeedImageFactory.ProcessedPath(GeneratedRoot, imageId));
        image.Mutate(x => x.Resize(240, 0));

        await SimulateWork();
        await image.SaveAsync(SeedImageFactory.ThumbnailPath(GeneratedRoot, imageId));
        logger.LogInformation("Generated thumbnail for image {ImageId}", imageId);
    }

    public async Task OptimizeImages(int imageId)
    {
        using var image = await Image.LoadAsync(SeedImageFactory.ProcessedPath(GeneratedRoot, imageId));

        await SimulateWork();
        await image.SaveAsync(
            SeedImageFactory.OptimizedPath(GeneratedRoot, imageId),
            new JpegEncoder { Quality = 55 });
        logger.LogInformation("Optimized image {ImageId} for web delivery", imageId);
    }

    private Task SimulateWork() => Task.Delay(options.Value.SimulatedWorkDuration);
}
