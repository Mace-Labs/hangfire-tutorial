namespace GamingSeeds.Hangfire.Demo.Data;

public class User
{
    public int Id { get; set; }
    public required string Email { get; set; }
    public bool IsConfirmed { get; set; }
    public DateTime RegisteredAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }
}
