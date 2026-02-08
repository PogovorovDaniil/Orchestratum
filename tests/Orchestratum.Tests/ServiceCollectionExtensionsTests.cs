using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Orchestratum.Extentions;

namespace Orchestratum.Tests;

public class ServiceCollectionExtensionsTests : PostgreSqlTestBase, IClassFixture<PostgreSqlFixture>
{
    public ServiceCollectionExtensionsTests(PostgreSqlFixture fixture) : base(fixture)
    {
    }
    [Fact]
    public void AddOchestrator_ShouldRegisterServices()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddOchestratum((sp, config) =>
        {
            config.ConfigureDbContext(opts => opts.UseNpgsql(ConnectionString));
        });

        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var orchestrator = serviceProvider.GetService<IOrchestratum>();
        orchestrator.Should().NotBeNull();

        var hostedService = services.FirstOrDefault(s => s.ServiceType == typeof(IHostedService));
        hostedService.Should().NotBeNull();
    }

    [Fact]
    public void AddOchestrator_ShouldConfigureOrchestrator()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuredPollingInterval = TimeSpan.FromSeconds(5);

        // Act
        services.AddOchestratum((sp, config) =>
        {
            config.ConfigureDbContext(opts => opts.UseNpgsql(ConnectionString));
            config.CommandPollingInterval = configuredPollingInterval;
            config.RegisterExecutor("test", (s, d, ct) => Task.CompletedTask);
        });

        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var orchestrator = serviceProvider.GetRequiredService<IOrchestratum>();
        orchestrator.Should().NotBeNull();
    }

    [Fact]
    public void AddOchestrator_ShouldRegisterAsSingleton()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddOchestratum((sp, config) =>
        {
            config.ConfigureDbContext(opts => opts.UseNpgsql(ConnectionString));
        });

        var serviceProvider = services.BuildServiceProvider();

        // Act
        var orchestrator1 = serviceProvider.GetRequiredService<IOrchestratum>();
        var orchestrator2 = serviceProvider.GetRequiredService<IOrchestratum>();

        // Assert
        orchestrator1.Should().BeSameAs(orchestrator2);
    }

    [Fact]
    public void AddOchestrator_WithServiceProvider_ShouldPassCorrectProvider()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<TestService>();
        IServiceProvider? capturedProvider = null;

        services.AddOchestratum((sp, config) =>
        {
            capturedProvider = sp;
            config.ConfigureDbContext(opts => opts.UseNpgsql(ConnectionString));
        });

        // Act
        var serviceProvider = services.BuildServiceProvider();
        var orchestrator = serviceProvider.GetRequiredService<IOrchestratum>();

        // Assert
        capturedProvider.Should().NotBeNull();
        var testService = capturedProvider!.GetService<TestService>();
        testService.Should().NotBeNull();
    }

    private class TestService { }
}
