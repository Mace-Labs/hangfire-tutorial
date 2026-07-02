using Hangfire;

namespace GamingSeeds.Hangfire.Demo.Jobs;

public interface IFlakyJobs
{
    [AutomaticRetry(Attempts = 3, DelaysInSeconds = [10, 20, 30])]
    Task CallUnreliableApi();
}
