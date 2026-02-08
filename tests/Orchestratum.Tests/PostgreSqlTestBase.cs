using Microsoft.EntityFrameworkCore;
using Orchestratum.Database;

namespace Orchestratum.Tests;

public abstract class PostgreSqlTestBase : IAsyncLifetime
{
    protected readonly PostgreSqlFixture Fixture;

    protected PostgreSqlTestBase(PostgreSqlFixture fixture)
    {
        Fixture = fixture;
    }

    protected string ConnectionString => Fixture.ConnectionString;

    internal DbContextOptions<OrchestratumDbContext> CreateDbContextOptions()
    {
        return Fixture.CreateDbContextOptions();
    }

    protected async Task CleanDatabase()
    {
        await Fixture.CleanDatabase();
    }

    public virtual Task InitializeAsync() => Task.CompletedTask;

    public virtual async Task DisposeAsync()
    {
        await CleanDatabase();
    }
}
