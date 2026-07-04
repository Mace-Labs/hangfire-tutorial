namespace GamingSeeds.Hangfire.Demo.Jobs;

public interface IUserMaintenanceJobs
{
    Task DeleteUnconfirmedUsers();
}
