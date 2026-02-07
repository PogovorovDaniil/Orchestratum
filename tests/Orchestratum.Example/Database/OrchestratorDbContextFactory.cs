using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Orchestratum.Database;

namespace Orchestratum.Example.Database;

/// <summary>
/// Design-time factory for creating OrchestratorDbContext instances during migrations.
/// This allows EF Core tools to create the context when running migration commands.
/// 
/// Usage:
///   dotnet ef migrations add InitialOrchestrator --context OrchestratorDbContext
///   dotnet ef database update --context OrchestratorDbContext
/// </summary>
public class OrchestratorDbContextFactory : IDesignTimeDbContextFactory<OrchestratorDbContext>
{
    public OrchestratorDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<OrchestratorDbContext>();

        optionsBuilder.UseNpgsql(
            "Host=localhost;Username=root;Password=root;Database=simpleOrchestrator", 
            opts => opts.MigrationsAssembly(typeof(OrchestratorDbContextFactory).Assembly));

        return new OrchestratorDbContext(optionsBuilder.Options);
    }
}
