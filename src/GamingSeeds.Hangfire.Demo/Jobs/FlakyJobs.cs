namespace GamingSeeds.Hangfire.Demo.Jobs;

public class FlakyJobs(ApiOutage outage, ILogger<FlakyJobs> logger) : IFlakyJobs
{
    public Task CallUnreliableApi()
    {
        if (outage.IsDown)
            throw new HttpRequestException("Upstream API returned 503 Service Unavailable");

        logger.LogInformation("Unreliable API call succeeded");
        return Task.CompletedTask;
    }
}
