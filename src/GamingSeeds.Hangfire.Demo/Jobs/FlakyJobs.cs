namespace GamingSeeds.Hangfire.Demo.Jobs;

// Deliberately fails so the dashboard shows Hangfire's automatic retries and the Failed state.
public class FlakyJobs(ILogger<FlakyJobs> logger) : IFlakyJobs
{
    public Task CallUnreliableApi()
    {
        logger.LogWarning("Calling the unreliable payment API...");
        throw new InvalidOperationException("Upstream API returned 503 Service Unavailable");
    }
}
