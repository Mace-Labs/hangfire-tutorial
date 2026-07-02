namespace GamingSeeds.Hangfire.Demo.Data;

public class SentEmail
{
    public int Id { get; set; }
    public required string To { get; set; }
    public required string Subject { get; set; }
    public DateTime SentAt { get; set; }
}
