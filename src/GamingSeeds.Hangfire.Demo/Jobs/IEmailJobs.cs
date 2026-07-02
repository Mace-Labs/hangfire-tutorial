using Hangfire;

namespace GamingSeeds.Hangfire.Demo.Jobs;

public interface IEmailJobs
{
    // Hangfire reads job-filter attributes from the enqueued method — the interface method
    // when you enqueue via Enqueue<IEmailJobs> — so [AutomaticRetry] must live here, not on the class.
    [AutomaticRetry(Attempts = 5)]
    Task SendConfirmationEmail(int userId);

    Task SendConfirmationReminder(int userId);
}
