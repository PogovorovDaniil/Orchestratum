using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Orchestratum.Database;

namespace Orchestratum.Example.Database;

public class OrchestratorDbContextFactory : IDesignTimeDbContextFactory<OrchDbContext>
{
    public OrchDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<OrchDbContext>();

        optionsBuilder.UseNpgsql(
            "Host=localhost;Username=root;Password=root;Database=orchestratum_example",
            opts => opts.MigrationsAssembly(typeof(OrchestratorDbContextFactory).Assembly.GetName().Name));

        return new OrchDbContext(optionsBuilder.Options, "ORCH_");
    }
}
