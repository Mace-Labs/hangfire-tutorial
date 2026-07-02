namespace GamingSeeds.Hangfire.Demo;

public class DemoOptions
{
    public const string SectionName = "DemoOptions";

    public TimeSpan ConfirmationReminderDelay { get; set; } = TimeSpan.FromSeconds(30);

    public int UnconfirmedAccountLifetimeDays { get; set; } = 7;

    public TimeSpan SimulatedWorkDuration { get; set; } = TimeSpan.FromSeconds(2);
}
