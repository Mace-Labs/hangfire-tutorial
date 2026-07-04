namespace GamingSeeds.Hangfire.Demo;

// Demo prop: simulates the third-party API being down, so retries have a real outage to recover from.
public class ApiOutage
{
    public bool IsDown { get; set; }
}
