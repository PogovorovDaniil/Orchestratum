using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Orchestratum.Database;

/// <summary>
/// Database context for Orchestratum command storage.
/// </summary>
public class OrchDbContext : DbContext
{
    private readonly string tablePrefix;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrchDbContext"/> class with the specified options and configuration.
    /// </summary>
    /// <param name="contextOptions">The options for this context.</param>
    /// <param name="configuration">The service configuration containing table prefix and other settings.</param>
    public OrchDbContext(DbContextOptions<OrchDbContext> contextOptions, OrchServiceConfiguration configuration) : base(contextOptions)
    {
        tablePrefix = configuration.TablePrefix;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OrchDbContext"/> class with the specified options and table prefix.
    /// </summary>
    /// <param name="contextOptions">The options for this context.</param>
    /// <param name="tablePrefix">The prefix to use for database table names. Default is "ORCH_".</param>
    public OrchDbContext(DbContextOptions<OrchDbContext> contextOptions, string tablePrefix = "ORCH_") : base(contextOptions)
    {
        this.tablePrefix = tablePrefix;
    }

    /// <summary>
    /// Gets or sets the DbSet for command entities.
    /// </summary>
    public DbSet<OrchCommandDbo> Commands { get; set; }

    /// <summary>
    /// Configures the entity mappings and database schema.
    /// </summary>
    /// <param name="modelBuilder">The model builder to configure.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OrchCommandDbo>(e =>
        {
            e.ToTable($"{tablePrefix}commands");
            e.Property(p => p.Id).ValueGeneratedNever();
            e.HasIndex(p => p.Target);
            e.HasIndex(p => p.IsRunning);
            e.HasIndex(p => p.IsCompleted);
            e.HasIndex(p => p.IsFailed);
        });
    }
}
