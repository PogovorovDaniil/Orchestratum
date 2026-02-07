using Microsoft.EntityFrameworkCore;

namespace Orchestratum.Tests;

public class OrchestratorConfigurationTests : PostgreSqlTestBase, IClassFixture<PostgreSqlFixture>
{
    public OrchestratorConfigurationTests(PostgreSqlFixture fixture) : base(fixture)
    {
    }
    [Fact]
    public void RegisterExecutor_ShouldAddExecutor()
    {
        // Arrange
        var configuration = new OrchestratorConfiguration();
        var executorKey = "test-executor";
        ExecutorDelegate executor = (sp, data, ct) => Task.CompletedTask;

        // Act
        configuration.RegisterExecutor(executorKey, executor);

        // Assert
        configuration.storedExecutors.Should().ContainKey(executorKey);
        configuration.storedExecutors[executorKey].Should().Be(executor);
    }

    [Fact]
    public void RegisterExecutor_ShouldReturnConfiguration()
    {
        // Arrange
        var configuration = new OrchestratorConfiguration();

        // Act
        var result = configuration.RegisterExecutor("test", (sp, data, ct) => Task.CompletedTask);

        // Assert
        result.Should().BeSameAs(configuration);
    }

    [Fact]
    public void ConfigureDbContext_ShouldSetContextOptions()
    {
        // Arrange
        var configuration = new OrchestratorConfiguration();

        // Act
        configuration.ConfigureDbContext(opts => opts.UseNpgsql(ConnectionString));

        // Assert
        configuration.contextOptions.Should().NotBeNull();
    }

    [Fact]
    public void ConfigureDbContext_ShouldReturnConfiguration()
    {
        // Arrange
        var configuration = new OrchestratorConfiguration();

        // Act
        var result = configuration.ConfigureDbContext(opts => opts.UseNpgsql(ConnectionString));

        // Assert
        result.Should().BeSameAs(configuration);
    }

    [Fact]
    public void DefaultValues_ShouldBeSetCorrectly()
    {
        // Arrange & Act
        var configuration = new OrchestratorConfiguration();

        // Assert
        configuration.CommandPollingInterval.Should().Be(TimeSpan.FromMinutes(1));
        configuration.LockTimeoutBuffer.Should().Be(TimeSpan.FromSeconds(1));
        configuration.DefaultTimeout.Should().Be(TimeSpan.FromMinutes(1));
        configuration.DefaultRetryCount.Should().Be(3);
    }

    [Fact]
    public void PropertySetters_ShouldUpdateValues()
    {
        // Arrange
        var configuration = new OrchestratorConfiguration();
        var pollingInterval = TimeSpan.FromSeconds(30);
        var lockTimeout = TimeSpan.FromSeconds(5);
        var defaultTimeout = TimeSpan.FromMinutes(5);
        var retryCount = 10;

        // Act
        configuration.CommandPollingInterval = pollingInterval;
        configuration.LockTimeoutBuffer = lockTimeout;
        configuration.DefaultTimeout = defaultTimeout;
        configuration.DefaultRetryCount = retryCount;

        // Assert
        configuration.CommandPollingInterval.Should().Be(pollingInterval);
        configuration.LockTimeoutBuffer.Should().Be(lockTimeout);
        configuration.DefaultTimeout.Should().Be(defaultTimeout);
        configuration.DefaultRetryCount.Should().Be(retryCount);
    }

    [Fact]
    public void FluentAPI_ShouldAllowChaining()
    {
        // Arrange
        var configuration = new OrchestratorConfiguration();

        // Act
        var result = configuration
            .RegisterExecutor("executor1", (sp, data, ct) => Task.CompletedTask)
            .RegisterExecutor("executor2", (sp, data, ct) => Task.CompletedTask)
            .ConfigureDbContext(opts => opts.UseNpgsql(ConnectionString));

        // Assert
        result.Should().BeSameAs(configuration);
        configuration.storedExecutors.Should().HaveCount(2);
    }
}
