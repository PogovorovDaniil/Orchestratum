using Microsoft.EntityFrameworkCore;

namespace Orchestratum.Database;

public class OrchestratumDbContext(DbContextOptions<OrchestratumDbContext> contextOptions) : DbContext(contextOptions)
{
    public DbSet<CommandDbo> Commands { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CommandDbo>(e =>
        {
            e.Property(p => p.Id).ValueGeneratedNever();
            e.HasIndex(p => p.IsRunning);
            e.HasIndex(p => p.IsCompleted);
            e.HasIndex(p => p.IsFailed);
        });
    }
}
