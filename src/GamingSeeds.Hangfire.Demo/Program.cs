using GamingSeeds.Hangfire.Demo;
using GamingSeeds.Hangfire.Demo.Data;
using GamingSeeds.Hangfire.Demo.Endpoints;
using GamingSeeds.Hangfire.Demo.Jobs;
using Hangfire;
using Hangfire.SqlServer;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<DemoOptions>(builder.Configuration.GetSection(DemoOptions.SectionName));

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("AppConnection")));

// Jobs are DI-resolved instance classes (never static) so they can take scoped dependencies like AppDbContext.
builder.Services.AddScoped<IEmailJobs, EmailJobs>();
builder.Services.AddScoped<IUserMaintenanceJobs, UserMaintenanceJobs>();
builder.Services.AddScoped<IImageProcessingJobs, ImageProcessingJobs>();

// Both poll intervals are lowered to 1 second so jobs, delays, and retries fire promptly
// on camera. The 15-second defaults are fine in production.
builder.Services.AddHangfire(config => config.UseSqlServerStorage(
    builder.Configuration.GetConnectionString("HangfireConnection"),
    new SqlServerStorageOptions { QueuePollInterval = TimeSpan.FromSeconds(1) }));

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

app.UseHangfireDashboard();

app.Services.GetRequiredService<IRecurringJobManager>().AddOrUpdate<IUserMaintenanceJobs>(
    "cleanup-unconfirmed-users",
    x => x.DeleteUnconfirmedUsers(),
    Cron.Daily);

app.MapUserEndpoints();
app.MapImageEndpoints();
app.MapDemoEndpoints();

app.Run();
