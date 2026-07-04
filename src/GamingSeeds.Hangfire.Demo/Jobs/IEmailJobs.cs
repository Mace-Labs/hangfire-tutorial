namespace GamingSeeds.Hangfire.Demo.Jobs;

public interface IEmailJobs
{
    Task SendConfirmationEmail(int userId);
}
