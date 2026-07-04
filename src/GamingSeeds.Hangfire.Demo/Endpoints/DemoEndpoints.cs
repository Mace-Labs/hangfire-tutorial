using GamingSeeds.Hangfire.Demo.Data;
using Microsoft.EntityFrameworkCore;

namespace GamingSeeds.Hangfire.Demo.Endpoints;

public static class DemoEndpoints
{
    public static IEndpointRouteBuilder MapDemoEndpoints(this IEndpointRouteBuilder app)
    {
        // Wipes demo data between recording takes.
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
