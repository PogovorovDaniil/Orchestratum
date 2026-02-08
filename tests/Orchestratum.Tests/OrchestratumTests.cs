using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Orchestratum.Database;

namespace Orchestratum.Tests;

public class OrchestratumTests : PostgreSqlTestBase, IClassFixture<PostgreSqlFixture>, IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly IOrchestratum _orchestrator;
    private readonly DbContextOptions<OrchestratumDbContext> _dbContextOptions;

    public OrchestratumTests(PostgreSqlFixture fixture) : base(fixture)
    {
        var services = new ServiceCollection();
        services.AddLogging();

        _dbContextOptions = CreateDbContextOptions();

        services.AddSingleton(_dbContextOptions);
        _serviceProvider = services.BuildServiceProvider();

        var configuration = new OrchestratumConfiguration
        {
            CommandPollingInterval = TimeSpan.FromMilliseconds(100),
            DefaultTimeout = TimeSpan.FromSeconds(5),
            DefaultRetryCount = 3,
            LockTimeoutBuffer = TimeSpan.FromSeconds(1)
        };

        configuration.ConfigureDbContext(opts => opts.UseNpgsql(ConnectionString));

        _orchestrator = new Services.Orchestratum(_serviceProvider, configuration);
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();
    }

    [Fact]
    public async Task Append_ShouldAddCommandToDatabase()
    {
        // Arrange
        var testData = new TestData { Value = "test" };
        var executorKey = "test-executor";

        ((Services.Orchestratum)_orchestrator).executors[executorKey] = (sp, data, ct) =>
        {
            return Task.CompletedTask;
        };

        // Act
        await _orchestrator.Append(executorKey, testData);

        // Assert
        using var context = new OrchestratumDbContext(_dbContextOptions);
        var commands = await context.Commands.ToListAsync();
        commands.Should().HaveCount(1);
        commands[0].Executor.Should().Be(executorKey);
        commands[0].IsCompleted.Should().BeFalse();
        commands[0].IsFailed.Should().BeFalse();
        commands[0].RetriesLeft.Should().Be(3);
    }

    [Fact]
    public async Task Append_WithCustomTimeout_ShouldSetCorrectTimeout()
    {
        // Arrange
        var testData = new TestData { Value = "test" };
        var executorKey = "test-executor";
        var customTimeout = TimeSpan.FromMinutes(10);

        ((Services.Orchestratum)_orchestrator).executors[executorKey] = (sp, data, ct) =>
            Task.CompletedTask;

        // Act
        await _orchestrator.Append(executorKey, testData, timeout: customTimeout);

        // Assert
        using var context = new OrchestratumDbContext(_dbContextOptions);
        var command = await context.Commands.FirstAsync();
        command.Timeout.Should().Be(customTimeout);
    }

    [Fact]
    public async Task Append_WithCustomRetryCount_ShouldSetCorrectRetryCount()
    {
        // Arrange
        var testData = new TestData { Value = "test" };
        var executorKey = "test-executor";
        var customRetryCount = 5;

        ((Services.Orchestratum)_orchestrator).executors[executorKey] = (sp, data, ct) =>
            Task.CompletedTask;

        // Act
        await _orchestrator.Append(executorKey, testData, retryCount: customRetryCount);

        // Assert
        using var context = new OrchestratumDbContext(_dbContextOptions);
        var command = await context.Commands.FirstAsync();
        command.RetriesLeft.Should().Be(customRetryCount);
    }

    [Fact]
    public async Task Append_WithUnregisteredExecutor_ShouldThrowException()
    {
        // Arrange
        var testData = new TestData { Value = "test" };
        var unregisteredKey = "unregistered-executor";

        // Act & Assert
        var act = async () => await _orchestrator.Append(unregisteredKey, testData);
        await act.Should().ThrowAsync<OrchestratumException>()
            .WithMessage($"*executor with type '{unregisteredKey}' is not registered*");
    }

    [Fact]
    public async Task SyncCommands_ShouldLoadPendingCommands()
    {
        // Arrange
        using (var context = new OrchestratumDbContext(_dbContextOptions))
        {
            context.Commands.Add(new CommandDbo
            {
                Id = Guid.NewGuid(),
                Executor = "test",
                Target = "default",
                DataType = typeof(TestData).AssemblyQualifiedName!,
                Data = "{\"Value\":\"test\"}",
                Timeout = TimeSpan.FromMinutes(1),
                RetriesLeft = 3,
                IsCompleted = false,
                IsFailed = false
            });
            await context.SaveChangesAsync();
        }

        // Act
        await _orchestrator.SyncCommands(CancellationToken.None);

        // Assert
        var orchestrator = (Services.Orchestratum)_orchestrator;
        orchestrator.commands.Should().HaveCount(1);
    }

    [Fact]
    public async Task SyncCommands_ShouldNotLoadCompletedCommands()
    {
        // Arrange
        using (var context = new OrchestratumDbContext(_dbContextOptions))
        {
            context.Commands.Add(new CommandDbo
            {
                Id = Guid.NewGuid(),
                Executor = "test",
                Target = "default",
                DataType = typeof(TestData).AssemblyQualifiedName!,
                Data = "{\"Value\":\"test\"}",
                Timeout = TimeSpan.FromMinutes(1),
                RetriesLeft = 3,
                IsCompleted = true,
                IsFailed = false
            });
            await context.SaveChangesAsync();
        }

        // Act
        await _orchestrator.SyncCommands(CancellationToken.None);

        // Assert
        var orchestrator = (Services.Orchestratum)_orchestrator;
        orchestrator.commands.Should().BeEmpty();
    }

    [Fact]
    public async Task SyncCommands_ShouldNotLoadFailedCommands()
    {
        // Arrange
        using (var context = new OrchestratumDbContext(_dbContextOptions))
        {
            context.Commands.Add(new CommandDbo
            {
                Id = Guid.NewGuid(),
                Executor = "test",
                Target = "default",
                DataType = typeof(TestData).AssemblyQualifiedName!,
                Data = "{\"Value\":\"test\"}",
                Timeout = TimeSpan.FromMinutes(1),
                RetriesLeft = 3,
                IsCompleted = false,
                IsFailed = true
            });
            await context.SaveChangesAsync();
        }

        // Act
        await _orchestrator.SyncCommands(CancellationToken.None);

        // Assert
        var orchestrator = (Services.Orchestratum)_orchestrator;
        orchestrator.commands.Should().BeEmpty();
    }

    [Fact]
    public async Task RunCommands_ShouldExecuteCommand()
    {
        // Arrange
        var testData = new TestData { Value = "test" };
        var executorKey = "test-executor";
        bool executorCalled = false;
        TestData? receivedData = null;

        ((Services.Orchestratum)_orchestrator).executors[executorKey] = (sp, data, ct) =>
        {
            executorCalled = true;
            receivedData = data as TestData;
            return Task.CompletedTask;
        };

        await _orchestrator.Append(executorKey, testData);
        await _orchestrator.SyncCommands(CancellationToken.None);

        // Act
        _orchestrator.RunCommands(CancellationToken.None);
        await WaitForCommandsToComplete();

        // Assert
        executorCalled.Should().BeTrue();
        receivedData.Should().NotBeNull();
        receivedData!.Value.Should().Be("test");
    }

    [Fact]
    public async Task RunCommands_OnSuccessfulExecution_ShouldMarkCommandAsCompleted()
    {
        // Arrange
        var testData = new TestData { Value = "test" };
        var executorKey = "test-executor";

        ((Services.Orchestratum)_orchestrator).executors[executorKey] = (sp, data, ct) =>
            Task.CompletedTask;

        await _orchestrator.Append(executorKey, testData);
        await _orchestrator.SyncCommands(CancellationToken.None);

        // Act
        _orchestrator.RunCommands(CancellationToken.None);
        await WaitForCommandsToComplete();

        // Assert
        using var context = new OrchestratumDbContext(_dbContextOptions);
        var command = await context.Commands.FirstAsync();
        command.IsCompleted.Should().BeTrue();
        command.CompleteAt.Should().NotBeNull();
        command.IsRunning.Should().BeFalse();
    }

    [Fact]
    public async Task RunCommands_OnException_ShouldDecrementRetries()
    {
        // Arrange
        var testData = new TestData { Value = "test" };
        var executorKey = "test-executor";

        ((Services.Orchestratum)_orchestrator).executors[executorKey] = (sp, data, ct) =>
            throw new Exception("Test exception");

        await _orchestrator.Append(executorKey, testData);
        await _orchestrator.SyncCommands(CancellationToken.None);

        // Act
        _orchestrator.RunCommands(CancellationToken.None);
        await WaitForCommandsToComplete();

        // Assert
        using var context = new OrchestratumDbContext(_dbContextOptions);
        var command = await context.Commands.FirstAsync();
        command.RetriesLeft.Should().Be(2); // Started with 3, decremented to 2
        command.IsCompleted.Should().BeFalse();
    }

    [Fact]
    public async Task RunCommands_AfterAllRetriesExhausted_ShouldMarkAsFailed()
    {
        // Arrange
        var testData = new TestData { Value = "test" };
        var executorKey = "test-executor";

        ((Services.Orchestratum)_orchestrator).executors[executorKey] = (sp, data, ct) =>
            throw new Exception("Test exception");

        await _orchestrator.Append(executorKey, testData, retryCount: 0);
        await _orchestrator.SyncCommands(CancellationToken.None);

        // Act
        _orchestrator.RunCommands(CancellationToken.None);
        await WaitForCommandsToComplete();

        // Assert
        using var context = new OrchestratumDbContext(_dbContextOptions);
        var command = await context.Commands.FirstAsync();
        command.IsFailed.Should().BeTrue();
        command.RetriesLeft.Should().Be(-1);
    }

    [Fact]
    public async Task WaitPollingInterval_ShouldWaitForConfiguredInterval()
    {
        // Arrange
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        await _orchestrator.WaitPollingInterval(CancellationToken.None);
        stopwatch.Stop();

        // Assert (allow some margin for timing differences)
        stopwatch.ElapsedMilliseconds.Should().BeGreaterThanOrEqualTo(90);
    }

    [Fact]
    public async Task WaitPollingInterval_WhenCommandAppended_ShouldCancelWait()
    {
        // Arrange
        var executorKey = "test-executor";
        ((Services.Orchestratum)_orchestrator).executors[executorKey] = (sp, data, ct) =>
            Task.CompletedTask;

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var waitTask = _orchestrator.WaitPollingInterval(CancellationToken.None);

        // Act
        await Task.Delay(50);
        await _orchestrator.Append(executorKey, new TestData { Value = "test" });
        await waitTask;
        stopwatch.Stop();

        // Assert
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(100);
    }

    private async Task WaitForCommandsToComplete(int timeoutMs = 5000)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var orchestrator = (Services.Orchestratum)_orchestrator;

        while (stopwatch.ElapsedMilliseconds < timeoutMs)
        {
            if (orchestrator.commands.All(c => !c.IsRunning))
                return;

            await Task.Delay(10);
        }
    }

    private async Task<bool> WaitForDatabaseCondition(Func<OrchestratumDbContext, Task<bool>> condition, int timeoutMs = 5000)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        while (stopwatch.ElapsedMilliseconds < timeoutMs)
        {
            using var context = new OrchestratumDbContext(_dbContextOptions);
            if (await condition(context))
                return true;

            await Task.Delay(10);
        }

        return false;
    }

    private class TestData
    {
        public string Value { get; set; } = string.Empty;
    }
}