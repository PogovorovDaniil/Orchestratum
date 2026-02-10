using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Orchestratum.Database;
using System.Collections.Concurrent;
using Testcontainers.PostgreSql;
using Xunit;

namespace Orchestratum.Tests.Misc;

public abstract class TestApplication : IAsyncLifetime
{
    private readonly PostgreSqlContainer postgresContainer = new PostgreSqlBuilder("postgres:18.1-alpine")
        .WithDatabase("orchestratum_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private readonly ConcurrentBag<string> _log = [];

    public void ClearLog() => _log.Clear();
    public List<string> GetLog() => _log.ToList();
    public void AddLog(string message) => _log.Add(message);

    public abstract void ConfigureOrchestratum(OrchServiceConfiguration configuration);

    public IServiceProvider ServiceProvider { get; private set; } = null!;
    public IOrchestratum Orchestratum { get => field ??= ServiceProvider.GetRequiredService<IOrchestratum>(); }
    public DbContextOptions<OrchDbContext> ContextOptions { get => field ??= ServiceProvider.GetRequiredService<DbContextOptions<OrchDbContext>>(); }

    public async Task InitializeAsync()
    {
        await postgresContainer.StartAsync();
        ServiceCollection services = new ServiceCollection();
        services.AddSingleton(this);
        services.AddOchestratum((config) =>
        {
            config.ConfigureDbContext(opt => opt.UseNpgsql(postgresContainer.GetConnectionString()));
            config.CommandPollingInterval = TimeSpan.FromMilliseconds(100);
            config.InstanceKey = "test-instance";
            ConfigureOrchestratum(config);
        });

        ServiceProvider = services.BuildServiceProvider();

        await CreatedDatabase();
        await CleanDatabase();
        var hostedServices = ServiceProvider.GetServices<IHostedService>();
        foreach (var hostedService in hostedServices) await hostedService.StartAsync(default);
    }

    public async Task DisposeAsync()
    {
        var hostedServices = ServiceProvider.GetServices<IHostedService>();
        foreach (var hostedService in hostedServices) await hostedService.StopAsync(default);
        await postgresContainer.DisposeAsync();
    }

    private async Task CreatedDatabase()
    {
        using var context = new OrchDbContext(ContextOptions, "ORCH_");
        await context.Database.EnsureCreatedAsync();
    }

    public async Task CleanDatabase()
    {
        using var context = new OrchDbContext(ContextOptions, "ORCH_");
        await context.Commands.ExecuteDeleteAsync();
    }
}
