using Microsoft.EntityFrameworkCore;

namespace Orchestratum.Database;

public class OrchestratorDbContext(DbContextOptions<OrchestratorDbContext> contextOptions) : DbContext(contextOptions)
{
    public DbSet<OrchestratorCommandDbo> Commands { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OrchestratorCommandDbo>(e =>
        {
            e.Property(p => p.Id).ValueGeneratedNever();
            e.HasIndex(p => p.IsRunning);
            e.HasIndex(p => p.IsCompleted);
            e.HasIndex(p => p.IsFailed);
        });
    }
}
