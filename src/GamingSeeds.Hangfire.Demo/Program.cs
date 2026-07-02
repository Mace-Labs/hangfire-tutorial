using GamingSeeds.Hangfire.Demo;
using GamingSeeds.Hangfire.Demo.Data;
using GamingSeeds.Hangfire.Demo.Jobs;
using Hangfire;
using Hangfire.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<DemoOptions>(builder.Configuration.GetSection(DemoOptions.SectionName));

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("AppConnection")));

// Jobs are DI-resolved instance classes (never static) so they can take scoped dependencies like AppDbContext.
builder.Services.AddScoped<IEmailJobs, EmailJobs>();
builder.Services.AddScoped<IUserMaintenanceJobs, UserMaintenanceJobs>();
builder.Services.AddScoped<IImageProcessingJobs, ImageProcessingJobs>();
builder.Services.AddScoped<IFlakyJobs, FlakyJobs>();

builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSqlServerStorage(builder.Configuration.GetConnectionString("HangfireConnection"), new SqlServerStorageOptions
    {
        QueuePollInterval = TimeSpan.FromSeconds(1),
        DisableGlobalLocks = true
    }));

// Poll for scheduled/retry jobs every second so delayed jobs and retries fire promptly in the demo.
// The default is 15 seconds, which is fine in production.
builder.Services.AddHangfireServer(options =>
    options.SchedulePollingInterval = TimeSpan.FromSeconds(1));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

Directory.CreateDirectory(Path.Combine(app.Environment.WebRootPath, "images", "generated"));

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseRouting();

app.UseHangfireDashboard();

app.Services.GetRequiredService<IRecurringJobManager>().AddOrUpdate<IUserMaintenanceJobs>(
    "cleanup-unconfirmed-users",
    x => x.DeleteUnconfirmedUsers(),
    Cron.Daily);

app.MapPost("/users/register", async (RegisterRequest request, AppDbContext db, IBackgroundJobClient jobs, IOptions<DemoOptions> options) =>
{
    var user = new User { Email = request.Email, RegisteredAt = DateTime.UtcNow };
    db.Users.Add(user);
    await db.SaveChangesAsync();

    // Fire-and-forget: the slow SMTP round-trip is moved off the request path.
    jobs.Enqueue<IEmailJobs>(x => x.SendConfirmationEmail(user.Id));

    // Delayed: nudge the user later if they still haven't confirmed.
    jobs.Schedule<IEmailJobs>(x => x.SendConfirmationReminder(user.Id), options.Value.ConfirmationReminderDelay);

    return Results.Ok(user);
});

app.MapPost("/users/{id:int}/confirm", async (int id, AppDbContext db) =>
{
    var user = await db.Users.FindAsync(id);
    if (user is null) return Results.NotFound();

    user.IsConfirmed = true;
    user.ConfirmedAt = DateTime.UtcNow;
    await db.SaveChangesAsync();
    return Results.Ok(user);
});

// Seeds already-stale unconfirmed accounts so the recurring cleanup has something to remove on camera.
app.MapPost("/users/seed-stale", async (AppDbContext db) =>
{
    for (var i = 0; i < 3; i++)
    {
        db.Users.Add(new User
        {
            Email = $"abandoned-{Guid.NewGuid():N}@example.com",
            RegisteredAt = DateTime.UtcNow.AddDays(-30)
        });
    }
    await db.SaveChangesAsync();
    return Results.Ok();
});

app.MapGet("/users", async (AppDbContext db) =>
    await db.Users.OrderByDescending(u => u.RegisteredAt).ToListAsync());

app.MapGet("/emails", async (AppDbContext db) =>
    await db.SentEmails.OrderByDescending(e => e.SentAt).Take(20).ToListAsync());

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

app.MapGet("/images", (IWebHostEnvironment env) =>
{
    var generatedRoot = Path.Combine(env.WebRootPath, "images", "generated");
    if (!Directory.Exists(generatedRoot)) return Results.Ok(Array.Empty<string>());

    string[] imageExtensions = [".png", ".jpg", ".jpeg"];
    var files = Directory.EnumerateFiles(generatedRoot)
        .Where(f => imageExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
        .OrderByDescending(File.GetCreationTimeUtc)
        .Select(f => "/images/generated/" + Path.GetFileName(f));

    return Results.Ok(files);
});

app.MapPost("/demo/fail", (IBackgroundJobClient jobs) =>
{
    jobs.Enqueue<IFlakyJobs>(x => x.CallUnreliableApi());
    return Results.Ok();
});

app.Run();

record RegisterRequest(string Email);
