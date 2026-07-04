using Hangfire;

namespace GamingSeeds.Hangfire.Demo.Jobs;

public interface IFlakyJobs
{
    // Short, growing back-offs so the retry demo is quick to watch; use larger values for real workloads.
    [AutomaticRetry(Attempts = 3, DelaysInSeconds = [3, 6, 9])]
    Task CallUnreliableApi();
}
