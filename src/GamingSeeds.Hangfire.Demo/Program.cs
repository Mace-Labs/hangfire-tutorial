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

builder.Services.AddScoped<EmailJobs>();

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

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseHangfireDashboard();

app.MapUserEndpoints();
app.MapDemoEndpoints();

app.Run();
