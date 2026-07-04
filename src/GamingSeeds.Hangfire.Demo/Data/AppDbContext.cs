using Microsoft.EntityFrameworkCore;

namespace GamingSeeds.Hangfire.Demo.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<SentEmail> SentEmails => Set<SentEmail>();
}
