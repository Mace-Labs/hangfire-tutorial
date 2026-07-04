using GamingSeeds.Hangfire.Demo.Data;
using GamingSeeds.Hangfire.Demo.Jobs;
using Hangfire;
using Microsoft.EntityFrameworkCore;

namespace GamingSeeds.Hangfire.Demo.Endpoints;

public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/users/register", async (RegisterRequest request, AppDbContext db, IBackgroundJobClient jobs) =>
        {
            var user = new User { Email = request.Email, RegisteredAt = DateTime.UtcNow };
            db.Users.Add(user);
            await db.SaveChangesAsync();

            // Fire-and-forget: the slow SMTP round-trip is moved off the request path.
            jobs.Enqueue<IEmailJobs>(x => x.SendConfirmationEmail(user.Id));

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

        app.MapGet("/users", async (AppDbContext db) =>
            await db.Users.OrderByDescending(u => u.RegisteredAt).ToListAsync());

        app.MapGet("/emails", async (AppDbContext db) =>
            await db.SentEmails.OrderByDescending(e => e.SentAt).Take(20).ToListAsync());

        return app;
    }
}

public record RegisterRequest(string Email);
