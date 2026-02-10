using Microsoft.EntityFrameworkCore;
using Orchestratum.Database;
using Orchestratum.Tests.Fixtures;
using Xunit;

namespace Orchestratum.Tests;

public abstract class TestBase : IClassFixture<TestFixture>, IAsyncLifetime
{
    protected readonly TestFixture Fixture;

    protected TestBase(TestFixture fixture)
    {
        Fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        Fixture.ClearLog();
        await Fixture.CleanDatabase();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    protected async Task<OrchCommandDbo?> GetCommandByIdAsync(Guid id)
    {
        using var context = new OrchDbContext(Fixture.ContextOptions, "ORCH_");
        return await context.Commands.FirstOrDefaultAsync(c => c.Id == id);
    }

    protected async Task<OrchCommandDbo> GetLastCommandAsync()
    {
        using var context = new OrchDbContext(Fixture.ContextOptions, "ORCH_");
        return await context.Commands.OrderByDescending(c => c.ScheduledAt).FirstAsync();
    }

    protected async Task<List<OrchCommandDbo>> GetAllCommandsAsync()
    {
        using var context = new OrchDbContext(Fixture.ContextOptions, "ORCH_");
        return await context.Commands.ToListAsync();
    }

    protected List<string> GetLog() => Fixture.GetLog();
}
