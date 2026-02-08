using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Orchestratum.Database;
using Orchestratum.Services;
using System.Diagnostics;

namespace Orchestratum.Tests;

public class OrchestratumCommandTests : PostgreSqlTestBase, IClassFixture<PostgreSqlFixture>
{
    private readonly ServiceProvider _serviceProvider;
    private readonly DbContextOptions<OrchestratumDbContext> _dbContextOptions;

    public OrchestratumCommandTests(PostgreSqlFixture fixture) : base(fixture)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        _dbContextOptions = CreateDbContextOptions();
        services.AddSingleton(_dbContextOptions);
        _serviceProvider = services.BuildServiceProvider();
    }

    [Fact]
    public async Task Run_ShouldExecuteCommandSuccessfully()
    {
        // Arrange
        var configuration = CreateConfiguration();
        bool executorCalled = false;
        TestData? receivedData = null;

        configuration.RegisterExecutor("test-executor", (sp, data, ct) =>
        {
            executorCalled = true;
            receivedData = data as TestData;
            return Task.CompletedTask;
        });

        var orchestrator = new Services.Orchestratum(_serviceProvider, configuration);
        var commandId = await CreateCommand("test-executor", new TestData { Value = "test" });
        var command = new CommandHelper(orchestrator, commandId);

        // Act
        command.Run();
        await WaitForCommandCompletion(command);

        // Assert
        executorCalled.Should().BeTrue();
        receivedData.Should().NotBeNull();
        receivedData!.Value.Should().Be("test");
        command.IsCompleted.Should().BeTrue();
        command.IsFailed.Should().BeFalse();
    }

    [Fact]
    public async Task Run_WhenCommandTimesOut_ShouldFailAndDecrementRetries()
    {
        // Arrange
        var configuration = CreateConfiguration();
        var tcs = new TaskCompletionSource();

        configuration.RegisterExecutor("slow-executor", async (sp, data, ct) =>
        {
            tcs.SetResult();
            await Task.Delay(TimeSpan.FromSeconds(10), ct); // Much longer than timeout
        });

        var orchestrator = new Services.Orchestratum(_serviceProvider, configuration);
        var commandId = await CreateCommand("slow-executor", new TestData { Value = "test" },
            timeout: TimeSpan.FromMilliseconds(100), retryCount: 2);
        var command = new CommandHelper(orchestrator, commandId);

        // Act
        command.Run();
        await tcs.Task; // Wait for executor to start
        await WaitForCommandCompletion(command);

        // Assert
        command.IsCompleted.Should().BeFalse();
        command.IsFailed.Should().BeFalse();

        using var context = new OrchestratumDbContext(_dbContextOptions);
        var cmd = await context.Commands.FindAsync(commandId);
        cmd!.RetriesLeft.Should().Be(1); // Started with 2, decremented to 1
        cmd.IsRunning.Should().BeFalse();
    }

    [Fact]
    public async Task Run_WhenCommandFailsAndRetriesExhausted_ShouldMarkAsFailed()
    {
        // Arrange
        var configuration = CreateConfiguration();
        configuration.RegisterExecutor("failing-executor", (sp, data, ct) =>
            throw new Exception("Test exception"));

        var orchestrator = new Services.Orchestratum(_serviceProvider, configuration);
        var commandId = await CreateCommand("failing-executor", new TestData { Value = "test" },
            retryCount: 0);
        var command = new CommandHelper(orchestrator, commandId);

        // Act
        command.Run();
        await WaitForCommandCompletion(command);

        // Assert
        command.IsCompleted.Should().BeFalse();
        command.IsFailed.Should().BeTrue();

        using var context = new OrchestratumDbContext(_dbContextOptions);
        var cmd = await context.Commands.FindAsync(commandId);
        cmd!.IsFailed.Should().BeTrue();
        cmd.RetriesLeft.Should().Be(-1);
    }

    [Fact]
    public async Task Run_WhenDisposedDuringExecution_ShouldCancelGracefully()
    {
        // Arrange
        var configuration = CreateConfiguration();
        var executionStarted = new TaskCompletionSource();
        var executionCancelled = false;

        configuration.RegisterExecutor("cancellable-executor", async (sp, data, ct) =>
        {
            executionStarted.SetResult();
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(10), ct);
            }
            catch (OperationCanceledException)
            {
                executionCancelled = true;
                throw;
            }
        });

        var orchestrator = new Services.Orchestratum(_serviceProvider, configuration);
        var commandId = await CreateCommand("cancellable-executor", new TestData { Value = "test" });
        var command = new CommandHelper(orchestrator, commandId);

        // Act
        command.Run();
        await executionStarted.Task;
        command.Dispose();
        await WaitForCommandCompletion(command);

        // Assert
        executionCancelled.Should().BeTrue();
        command.IsCompleted.Should().BeFalse();
    }

    [Fact]
    public async Task Run_WhenAlreadyRunning_ShouldThrowException()
    {
        // Arrange
        var configuration = CreateConfiguration();
        var executionStarted = new TaskCompletionSource();

        configuration.RegisterExecutor("long-executor", async (sp, data, ct) =>
        {
            executionStarted.SetResult();
            await Task.Delay(TimeSpan.FromSeconds(10), ct);
        });

        var orchestrator = new Services.Orchestratum(_serviceProvider, configuration);
        var commandId = await CreateCommand("long-executor", new TestData { Value = "test" });
        var command = new CommandHelper(orchestrator, commandId);

        command.Run();
        await executionStarted.Task;

        // Act & Assert
        var act = () => command.Run();
        act.Should().Throw<OrchestratumException>()
            .WithMessage("*already running*");

        command.Dispose();
    }

    [Fact]
    public async Task RunLock_WhenCommandIsAlreadyCompleted_ShouldNotAcquireLock()
    {
        // Arrange
        var configuration = CreateConfiguration();
        configuration.RegisterExecutor("test-executor", (sp, data, ct) => Task.CompletedTask);

        var orchestrator = new Services.Orchestratum(_serviceProvider, configuration);
        var commandId = await CreateCommand("test-executor", new TestData { Value = "test" });

        // Mark as completed
        using (var context = new OrchestratumDbContext(_dbContextOptions))
        {
            await context.Commands
                .Where(c => c.Id == commandId)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.IsCompleted, true));
        }

        var command = new CommandHelper(orchestrator, commandId);

        // Act
        command.Run();
        await Task.Delay(200); // Give it time to attempt execution

        // Assert
        command.IsRunning.Should().BeFalse();
        command.IsCompleted.Should().BeFalse(); // Didn't execute, so local state unchanged
    }

    [Fact]
    public async Task RunLock_WhenLockHasExpired_ShouldAllowRetry()
    {
        // Arrange
        var configuration = CreateConfiguration();
        int executionCount = 0;

        configuration.RegisterExecutor("test-executor", (sp, data, ct) =>
        {
            executionCount++;
            return Task.CompletedTask;
        });

        var orchestrator = new Services.Orchestratum(_serviceProvider, configuration);
        var commandId = await CreateCommand("test-executor", new TestData { Value = "test" });

        // Simulate expired lock
        using (var context = new OrchestratumDbContext(_dbContextOptions))
        {
            await context.Commands
                .Where(c => c.Id == commandId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(c => c.IsRunning, true)
                    .SetProperty(c => c.RunExpiresAt, DateTimeOffset.UtcNow.AddMinutes(-1)));
        }

        var command = new CommandHelper(orchestrator, commandId);

        // Act
        command.Run();
        await WaitForCommandCompletion(command);

        // Assert
        executionCount.Should().Be(1); // Should have executed despite being marked as running
        command.IsCompleted.Should().BeTrue();
    }

    [Fact]
    public async Task Run_WithInvalidDataType_ShouldMarkAsFailed()
    {
        // Arrange
        var configuration = CreateConfiguration();
        configuration.RegisterExecutor("test-executor", (sp, data, ct) => Task.CompletedTask);

        var orchestrator = new Services.Orchestratum(_serviceProvider, configuration);

        // Create command with invalid type
        var commandDbo = new CommandDbo
        {
            Executor = "test-executor",
            Target = "default",
            DataType = "InvalidType.DoesNotExist, NonExistentAssembly",
            Data = "{\"Value\":\"test\"}",
            Timeout = TimeSpan.FromMinutes(1),
            RetriesLeft = 0
        };

        using (var context = new OrchestratumDbContext(_dbContextOptions))
        {
            context.Commands.Add(commandDbo);
            await context.SaveChangesAsync();
        }

        var command = new CommandHelper(orchestrator, commandDbo.Id);

        // Act
        command.Run();
        await WaitForCommandCompletion(command);

        // Assert
        command.IsFailed.Should().BeTrue();
    }

    [Fact]
    public async Task ParallelExecution_ShouldNotProcessSameCommandTwice()
    {
        // Arrange
        var configuration = CreateConfiguration();
        int executionCount = 0;
        var executionLock = new object();
        var executionStarted = new TaskCompletionSource();

        configuration.RegisterExecutor("test-executor", async (sp, data, ct) =>
        {
            lock (executionLock)
            {
                executionCount++;
            }
            executionStarted.TrySetResult();
            await Task.Delay(100, ct);
        });

        var orchestrator = new Services.Orchestratum(_serviceProvider, configuration);
        var commandId = await CreateCommand("test-executor", new TestData { Value = "test" });

        var command1 = new CommandHelper(orchestrator, commandId);
        var command2 = new CommandHelper(orchestrator, commandId);

        // Act - Try to run the same command twice in parallel
        command1.Run();
        await Task.Delay(10); // Small delay to ensure first one starts
        command2.Run();

        await WaitForCommandCompletion(command1);
        await WaitForCommandCompletion(command2);

        // Assert
        executionCount.Should().Be(1); // Should only execute once
        (command1.IsCompleted || command2.IsCompleted).Should().BeTrue();
    }

    [Fact]
    public async Task Run_ShouldSetRunExpiresAtCorrectly()
    {
        // Arrange
        var configuration = CreateConfiguration();
        var executionStarted = new TaskCompletionSource();

        configuration.RegisterExecutor("test-executor", async (sp, data, ct) =>
        {
            executionStarted.SetResult();
            await Task.Delay(50, ct);
        });

        var orchestrator = new Services.Orchestratum(_serviceProvider, configuration);
        var timeout = TimeSpan.FromMinutes(5);
        var commandId = await CreateCommand("test-executor", new TestData { Value = "test" }, timeout: timeout);
        var command = new CommandHelper(orchestrator, commandId);

        var beforeRun = DateTimeOffset.UtcNow;

        // Act
        command.Run();
        await executionStarted.Task;

        // Assert
        using var context = new OrchestratumDbContext(_dbContextOptions);
        var cmd = await context.Commands.AsNoTracking().FirstAsync(c => c.Id == commandId);

        cmd.IsRunning.Should().BeTrue();
        cmd.RunExpiresAt.Should().NotBeNull();
        cmd.RunExpiresAt.Should().BeAfter(beforeRun + timeout);

        command.Dispose();
    }

    // Helper methods

    private OrchestratumConfiguration CreateConfiguration()
    {
        var configuration = new OrchestratumConfiguration
        {
            CommandPollingInterval = TimeSpan.FromMilliseconds(100),
            DefaultTimeout = TimeSpan.FromSeconds(5),
            DefaultRetryCount = 3,
            LockTimeoutBuffer = TimeSpan.FromSeconds(1)
        };

        configuration.ConfigureDbContext(opts => opts.UseNpgsql(ConnectionString));
        return configuration;
    }

    private async Task<Guid> CreateCommand(string executorKey, object data,
        TimeSpan? timeout = null, int? retryCount = null)
    {
        var commandDbo = new CommandDbo
        {
            Executor = executorKey,
            Target = "default",
            DataType = data.GetType().AssemblyQualifiedName!,
            Data = System.Text.Json.JsonSerializer.Serialize(data),
            Timeout = timeout ?? TimeSpan.FromMinutes(1),
            RetriesLeft = retryCount ?? 3
        };

        using var context = new OrchestratumDbContext(_dbContextOptions);
        context.Commands.Add(commandDbo);
        await context.SaveChangesAsync();
        return commandDbo.Id;
    }

    private static async Task WaitForCommandCompletion(CommandHelper command, int timeoutMs = 5000)
    {
        var stopwatch = Stopwatch.StartNew();
        while (command.IsRunning && stopwatch.ElapsedMilliseconds < timeoutMs)
        {
            await Task.Delay(10);
        }
    }

    private class TestData
    {
        public string Value { get; set; } = string.Empty;
    }
}
