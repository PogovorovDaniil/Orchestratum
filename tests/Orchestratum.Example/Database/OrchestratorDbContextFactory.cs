using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Orchestratum.Database;

namespace Orchestratum.Example.Database;

/// <summary>
/// Design-time factory for creating OrchestratorDbContext instances during migrations.
/// This allows EF Core tools to create the context when running migration commands.
/// 
/// Usage:
///   dotnet ef migrations add InitialOrchestratum --context OrchestratumDbContext
///   dotnet ef database update --context OrchestratumDbContext
/// </summary>
public class OrchestratorDbContextFactory : IDesignTimeDbContextFactory<OrchestratumDbContext>
{
    public OrchestratumDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<OrchestratumDbContext>();

        optionsBuilder.UseNpgsql(
            "Host=localhost;Username=root;Password=root;Database=orchestratum_example",
            opts => opts.MigrationsAssembly(typeof(OrchestratorDbContextFactory).Assembly));

        return new OrchestratumDbContext(optionsBuilder.Options);
    }
}
