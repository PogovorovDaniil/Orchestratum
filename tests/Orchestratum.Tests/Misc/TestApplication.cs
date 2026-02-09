using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Orchestratum.Database;
using Orchestratum.Extentions;
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

    public TestApplication()
    {
        ServiceCollection services = new ServiceCollection();
        services.AddOchestratum((sp, config) =>
        {
            config.ConfigureDbContext(opt => opt.UseNpgsql(postgresContainer.GetConnectionString()));
            config.CommandPollingInterval = TimeSpan.FromMilliseconds(100);
            config.DefaultTimeout = TimeSpan.FromSeconds(30);
            config.DefaultRetryCount = 3;
            config.InstanceKey = "test-instance";
            ConfigureOrchestratum(sp, config);
        });

        ServiceProvider = services.BuildServiceProvider();
    }

    public virtual void ConfigureOrchestratum(IServiceProvider serviceProvider, OrchestratumConfiguration configuration) { }

    public IServiceProvider ServiceProvider { get; }
    public IOrchestratum Orchestratum { get => field ??= ServiceProvider.GetRequiredService<IOrchestratum>(); }
    public DbContextOptions<OrchestratumDbContext> ContextOptions { get => field ??= ServiceProvider.GetRequiredService<DbContextOptions<OrchestratumDbContext>>(); }

    public async Task InitializeAsync()
    {
        await postgresContainer.StartAsync();
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
        using var context = new OrchestratumDbContext(ContextOptions);
        await context.Database.EnsureCreatedAsync();
    }

    public async Task CleanDatabase()
    {
        using var context = new OrchestratumDbContext(ContextOptions);
        await context.Commands.ExecuteDeleteAsync();
    }
}
