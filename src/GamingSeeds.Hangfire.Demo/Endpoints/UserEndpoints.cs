using GamingSeeds.Hangfire.Demo.Data;
using GamingSeeds.Hangfire.Demo.Jobs;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GamingSeeds.Hangfire.Demo.Endpoints;

public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
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

        // The "before Hangfire" version: the same email method, but awaited during the
        // request — so the caller sits through the whole SMTP round-trip.
        app.MapPost("/users/register-inline", async (RegisterRequest request, AppDbContext db, IEmailJobs emailJobs) =>
        {
            var user = new User { Email = request.Email, RegisteredAt = DateTime.UtcNow };
            db.Users.Add(user);
            await db.SaveChangesAsync();

            await emailJobs.SendConfirmationEmail(user.Id);

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

        return app;
    }
}

public record RegisterRequest(string Email);
