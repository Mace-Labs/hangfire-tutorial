using GamingSeeds.Hangfire.Demo.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GamingSeeds.Hangfire.Demo.Jobs;

public class UserMaintenanceJobs(
    AppDbContext db,
    IOptions<DemoOptions> options,
    ILogger<UserMaintenanceJobs> logger) : IUserMaintenanceJobs
{
    public async Task DeleteUnconfirmedUsers()
    {
        var cutoff = DateTime.UtcNow.AddDays(-options.Value.UnconfirmedAccountLifetimeDays);

        var stale = await db.Users
            .Where(u => !u.IsConfirmed && u.RegisteredAt < cutoff)
            .ToListAsync();

        if (stale.Count == 0)
        {
            logger.LogInformation("Cleanup ran: no unconfirmed accounts past the {Days}-day cutoff",
                options.Value.UnconfirmedAccountLifetimeDays);
            return;
        }

        db.Users.RemoveRange(stale);
        await db.SaveChangesAsync();

        logger.LogInformation("Cleanup removed {Count} unconfirmed account(s)", stale.Count);
    }
}
