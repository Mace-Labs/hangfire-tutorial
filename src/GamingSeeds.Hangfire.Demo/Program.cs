using GamingSeeds.Hangfire.Demo;
using GamingSeeds.Hangfire.Demo.Data;
using GamingSeeds.Hangfire.Demo.Endpoints;
using GamingSeeds.Hangfire.Demo.Jobs;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<DemoOptions>(builder.Configuration.GetSection(DemoOptions.SectionName));

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("AppConnection")));

builder.Services.AddScoped<EmailJobs>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapUserEndpoints();
app.MapDemoEndpoints();

app.Run();
