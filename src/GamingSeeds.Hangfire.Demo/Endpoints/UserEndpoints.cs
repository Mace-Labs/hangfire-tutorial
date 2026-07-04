using GamingSeeds.Hangfire.Demo.Data;
using GamingSeeds.Hangfire.Demo.Jobs;
using Microsoft.EntityFrameworkCore;

namespace GamingSeeds.Hangfire.Demo.Endpoints;

public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        // The email is sent inline, during the request — the caller sits through the whole SMTP round-trip.
        app.MapPost("/users/register-inline", async (RegisterRequest request, AppDbContext db, EmailJobs emailJobs) =>
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
