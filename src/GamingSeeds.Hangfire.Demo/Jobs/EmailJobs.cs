using GamingSeeds.Hangfire.Demo.Data;
using Microsoft.Extensions.Options;

namespace GamingSeeds.Hangfire.Demo.Jobs;

public class EmailJobs(
    AppDbContext db,
    IOptions<DemoOptions> options,
    ILogger<EmailJobs> logger)
{
    private readonly DemoOptions _options = options.Value;

    public async Task SendConfirmationEmail(int userId)
    {
        var user = await db.Users.FindAsync(userId);
        if (user is null)
        {
            logger.LogWarning("Confirmation email skipped: user {UserId} no longer exists", userId);
            return;
        }

        await SimulateSmtpSend(user.Email, "Confirm your GamingSeeds account");
    }

    private async Task SimulateSmtpSend(string to, string subject)
    {
        logger.LogInformation("Sending '{Subject}' to {To}...", subject, to);
        await Task.Delay(_options.SimulatedWorkDuration);

        db.SentEmails.Add(new SentEmail { To = to, Subject = subject, SentAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        logger.LogInformation("Sent '{Subject}' to {To}", subject, to);
    }
}
