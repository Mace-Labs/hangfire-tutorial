using GamingSeeds.Hangfire.Demo.Jobs;
using Hangfire;

namespace GamingSeeds.Hangfire.Demo.Endpoints;

public static class ImageEndpoints
{
    public static IEndpointRouteBuilder MapImageEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/images/upload", async (IWebHostEnvironment env, IBackgroundJobClient jobs) =>
        {
            var imageId = Random.Shared.Next(10000, 99999);
            var generatedRoot = Path.Combine(env.WebRootPath, "images", "generated");
            await SeedImageFactory.CreateSource(SeedImageFactory.SourcePath(generatedRoot, imageId), imageId);

            // Continuation: each step only runs after the previous one succeeds.
            var processId = jobs.Enqueue<IImageProcessingJobs>(x => x.ProcessImage(imageId));
            var thumbId = jobs.ContinueJobWith<IImageProcessingJobs>(processId, x => x.GenerateThumbnails(imageId));
            jobs.ContinueJobWith<IImageProcessingJobs>(thumbId, x => x.OptimizeImages(imageId));

            return Results.Ok(new { imageId });
        });

        // Newest uploads first, each with a slot per pipeline step so the UI can show
        // the continuation chain filling in left to right.
        app.MapGet("/images", (IWebHostEnvironment env) =>
        {
            var generatedRoot = Path.Combine(env.WebRootPath, "images", "generated");
            if (!Directory.Exists(generatedRoot)) return Results.Ok(Array.Empty<object>());

            var uploads = Directory.EnumerateFiles(generatedRoot, "seed-*-source.png")
                .Select(f => new
                {
                    Id = int.Parse(Path.GetFileName(f).Split('-')[1]),
                    CreatedAt = File.GetCreationTimeUtc(f)
                })
                .OrderByDescending(x => x.CreatedAt)
                .Take(5)
                .Select(x => new
                {
                    imageId = x.Id,
                    steps = new
                    {
                        source = StepInfo(SeedImageFactory.SourcePath(generatedRoot, x.Id)),
                        processed = StepInfo(SeedImageFactory.ProcessedPath(generatedRoot, x.Id)),
                        thumbnail = StepInfo(SeedImageFactory.ThumbnailPath(generatedRoot, x.Id)),
                        optimized = StepInfo(SeedImageFactory.OptimizedPath(generatedRoot, x.Id))
                    }
                })
                .ToList();

            return Results.Ok(uploads);
        });

        return app;
    }

    private static object? StepInfo(string path)
    {
        var file = new FileInfo(path);
        return file.Exists
            ? new { url = "/images/generated/" + file.Name, kb = Math.Max(1, file.Length / 1024) }
            : null;
    }
}
