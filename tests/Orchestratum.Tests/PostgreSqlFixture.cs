using Microsoft.EntityFrameworkCore;
using Orchestratum.Database;
using Testcontainers.PostgreSql;

namespace Orchestratum.Tests;

public class PostgreSqlFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("orchestratum_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        ConnectionString = _container.GetConnectionString();

        var options = new DbContextOptionsBuilder<OrchestratumDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        using var context = new OrchestratumDbContext(options);
        await context.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    internal DbContextOptions<OrchestratumDbContext> CreateDbContextOptions()
    {
        return new DbContextOptionsBuilder<OrchestratumDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;
    }

    public async Task CleanDatabase()
    {
        var options = CreateDbContextOptions();
        using var context = new OrchestratumDbContext(options);
        await context.Database.ExecuteSqlRawAsync("TRUNCATE TABLE orchestratum_commands RESTART IDENTITY CASCADE");
    }
}
