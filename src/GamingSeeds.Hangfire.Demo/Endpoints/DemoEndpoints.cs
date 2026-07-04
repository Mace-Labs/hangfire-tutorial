using GamingSeeds.Hangfire.Demo.Data;
using GamingSeeds.Hangfire.Demo.Jobs;
using Hangfire;
using Microsoft.EntityFrameworkCore;

namespace GamingSeeds.Hangfire.Demo.Endpoints;

public static class DemoEndpoints
{
    public static IEndpointRouteBuilder MapDemoEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/demo/call-unreliable-api", (IBackgroundJobClient jobs) =>
        {
            jobs.Enqueue<IFlakyJobs>(x => x.CallUnreliableApi());
            return Results.Ok();
        });

        app.MapGet("/demo/outage", (ApiOutage outage) => Results.Ok(new { down = outage.IsDown }));

        app.MapPost("/demo/outage", (ApiOutage outage) =>
        {
            outage.IsDown = !outage.IsDown;
            return Results.Ok(new { down = outage.IsDown });
        });

        // Wipes demo data between recording takes. Hangfire's own job history is left alone.
        app.MapPost("/demo/reset", async (AppDbContext db, IWebHostEnvironment env) =>
        {
            await db.SentEmails.ExecuteDeleteAsync();
            await db.Users.ExecuteDeleteAsync();

            var generatedRoot = Path.Combine(env.WebRootPath, "images", "generated");
            if (Directory.Exists(generatedRoot))
            {
                foreach (var file in Directory.EnumerateFiles(generatedRoot, "seed-*"))
                    File.Delete(file);
            }

            return Results.Ok();
        });

        return app;
    }
}
