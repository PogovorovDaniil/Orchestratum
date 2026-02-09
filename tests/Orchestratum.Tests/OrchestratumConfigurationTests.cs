using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Orchestratum.Tests;

public class OrchestratumConfigurationTests
{
    [Fact]
    public void RegisterExecutor_ValidExecutor_RegistersSuccessfully()
    {
        // Arrange
        var config = new OrchestratumConfiguration();
        var executorKey = "test-executor";
        ExecutorDelegate executor = (sp, data, ct) => Task.CompletedTask;

        // Act
        var result = config.RegisterExecutor(executorKey, executor);

        // Assert
        Assert.Same(config, result); // Fluent API returns config for chaining
    }

    [Fact]
    public void RegisterExecutor_MultipleExecutors_ChainsProperly()
    {
        // Arrange
        var config = new OrchestratumConfiguration();

        // Act
        var result = config
            .RegisterExecutor("executor1", (sp, data, ct) => Task.CompletedTask)
            .RegisterExecutor("executor2", (sp, data, ct) => Task.CompletedTask)
            .RegisterExecutor("executor3", (sp, data, ct) => Task.CompletedTask);

        // Assert - Just verify fluent API works
        Assert.Same(config, result);
    }

    [Fact]
    public void ConfigureDbContext_ValidConfiguration_ReturnsConfig()
    {
        // Arrange
        var config = new OrchestratumConfiguration();

        // Act
        var result = config.ConfigureDbContext(opts => 
            opts.UseNpgsql("Host=localhost;Database=test"));

        // Assert
        Assert.Same(config, result); // Fluent API
    }

    [Fact]
    public void DefaultValues_NewConfiguration_HasCorrectDefaults()
    {
        // Arrange & Act
        var config = new OrchestratumConfiguration();

        // Assert
        Assert.Equal(TimeSpan.FromMinutes(1), config.CommandPollingInterval);
        Assert.Equal(TimeSpan.FromSeconds(1), config.LockTimeoutBuffer);
        Assert.Equal(TimeSpan.FromMinutes(1), config.DefaultTimeout);
        Assert.Equal(3, config.DefaultRetryCount);
        Assert.Equal("default", config.InstanceKey);
    }

    [Fact]
    public void CustomValues_SetProperties_AppliesCorrectly()
    {
        // Arrange
        var config = new OrchestratumConfiguration();

        // Act
        config.CommandPollingInterval = TimeSpan.FromSeconds(30);
        config.LockTimeoutBuffer = TimeSpan.FromSeconds(5);
        config.DefaultTimeout = TimeSpan.FromMinutes(5);
        config.DefaultRetryCount = 10;
        config.InstanceKey = "custom-instance";

        // Assert
        Assert.Equal(TimeSpan.FromSeconds(30), config.CommandPollingInterval);
        Assert.Equal(TimeSpan.FromSeconds(5), config.LockTimeoutBuffer);
        Assert.Equal(TimeSpan.FromMinutes(5), config.DefaultTimeout);
        Assert.Equal(10, config.DefaultRetryCount);
        Assert.Equal("custom-instance", config.InstanceKey);
    }

    [Fact]
    public void FluentConfiguration_ChainedCalls_WorksCorrectly()
    {
        // Arrange
        var config = new OrchestratumConfiguration();

        // Act
        var result = config
            .ConfigureDbContext(opts => opts.UseNpgsql("Host=localhost;Database=test"))
            .RegisterExecutor("exec1", (sp, data, ct) => Task.CompletedTask)
            .RegisterExecutor("exec2", (sp, data, ct) => Task.CompletedTask);

        config.CommandPollingInterval = TimeSpan.FromSeconds(10);
        config.DefaultRetryCount = 5;

        // Assert
        Assert.Same(config, result);
        Assert.Equal(TimeSpan.FromSeconds(10), config.CommandPollingInterval);
        Assert.Equal(5, config.DefaultRetryCount);
    }
}
