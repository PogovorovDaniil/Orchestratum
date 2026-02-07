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
        services.AddOchestrator((sp, config) =>
        {
            config.ConfigureDbContext(opts => opts.UseNpgsql(ConnectionString));
        });

        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var orchestrator = serviceProvider.GetService<IOrchestrator>();
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
        services.AddOchestrator((sp, config) =>
        {
            config.ConfigureDbContext(opts => opts.UseNpgsql(ConnectionString));
            config.CommandPollingInterval = configuredPollingInterval;
            config.RegisterExecutor("test", (s, d, ct) => Task.CompletedTask);
        });

        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var orchestrator = serviceProvider.GetRequiredService<IOrchestrator>();
        orchestrator.Should().NotBeNull();
    }

    [Fact]
    public void AddOchestrator_ShouldRegisterAsSingleton()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddOchestrator((sp, config) =>
        {
            config.ConfigureDbContext(opts => opts.UseNpgsql(ConnectionString));
        });

        var serviceProvider = services.BuildServiceProvider();

        // Act
        var orchestrator1 = serviceProvider.GetRequiredService<IOrchestrator>();
        var orchestrator2 = serviceProvider.GetRequiredService<IOrchestrator>();

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

        services.AddOchestrator((sp, config) =>
        {
            capturedProvider = sp;
            config.ConfigureDbContext(opts => opts.UseNpgsql(ConnectionString));
        });

        // Act
        var serviceProvider = services.BuildServiceProvider();
        var orchestrator = serviceProvider.GetRequiredService<IOrchestrator>();

        // Assert
        capturedProvider.Should().NotBeNull();
        var testService = capturedProvider!.GetService<TestService>();
        testService.Should().NotBeNull();
    }

    private class TestService { }
}
