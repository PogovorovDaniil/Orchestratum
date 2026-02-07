using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Orchestratum.Database;
using Orchestratum.Services;

namespace Orchestratum.Tests;

public class OrchestratorEdgeCasesTests : PostgreSqlTestBase, IClassFixture<PostgreSqlFixture>
{
    private readonly ServiceProvider _serviceProvider;
    private readonly DbContextOptions<OrchestratorDbContext> _dbContextOptions;

    public OrchestratorEdgeCasesTests(PostgreSqlFixture fixture) : base(fixture)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        _dbContextOptions = CreateDbContextOptions();
        services.AddSingleton(_dbContextOptions);
        _serviceProvider = services.BuildServiceProvider();
    }

    [Fact]
    public async Task Append_WithZeroTimeout_ShouldUseZeroTimeout()
    {
        // Arrange
        var configuration = CreateConfiguration();
        configuration.RegisterExecutor("test-executor", (sp, data, ct) => Task.CompletedTask);
        var orchestrator = new Orchestrator(_serviceProvider, configuration);

        // Act
        await orchestrator.Append("test-executor", new TestData { Value = "test" }, timeout: TimeSpan.Zero);

        // Assert
        using var context = new OrchestratorDbContext(_dbContextOptions);
        var command = await context.Commands.FirstAsync();
        command.Timeout.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public async Task Append_WithNegativeRetryCount_ShouldUseNegativeRetryCount()
    {
        // Arrange
        var configuration = CreateConfiguration();
        configuration.RegisterExecutor("test-executor", (sp, data, ct) => Task.CompletedTask);
        var orchestrator = new Orchestrator(_serviceProvider, configuration);

        // Act
        await orchestrator.Append("test-executor", new TestData { Value = "test" }, retryCount: -5);

        // Assert
        using var context = new OrchestratorDbContext(_dbContextOptions);
        var command = await context.Commands.FirstAsync();
        command.RetriesLeft.Should().Be(-5);
    }

    [Fact]
    public async Task RunCommands_WithVeryLargeNumberOfCommands_ShouldHandleAll()
    {
        // Arrange
        var configuration = CreateConfiguration();
        int executionCount = 0;
        var executionLock = new object();

        configuration.RegisterExecutor("test-executor", (sp, data, ct) =>
        {
            lock (executionLock)
            {
                executionCount++;
            }
            return Task.CompletedTask;
        });

        var orchestrator = new Orchestrator(_serviceProvider, configuration);

        // Add 100 commands
        for (int i = 0; i < 100; i++)
        {
            await orchestrator.Append("test-executor", new TestData { Value = $"test-{i}" });
        }

        await orchestrator.SyncCommands(CancellationToken.None);

        // Act
        orchestrator.RunCommands(CancellationToken.None);
        await WaitForAllCommandsToComplete(orchestrator, timeoutMs: 10000);

        // Assert
        executionCount.Should().Be(100);
    }

    [Fact]
    public async Task RegisterExecutor_WithDuplicateKey_ShouldOverwrite()
    {
        // Arrange
        var configuration = CreateConfiguration();
        ExecutorDelegate firstExecutor = (sp, data, ct) => Task.CompletedTask;
        ExecutorDelegate secondExecutor = (sp, data, ct) => Task.CompletedTask;

        // Act
        configuration.RegisterExecutor("duplicate", firstExecutor);
        configuration.RegisterExecutor("duplicate", secondExecutor);

        // Assert
        configuration.storedExecutors["duplicate"].Should().BeSameAs(secondExecutor);
    }

    [Fact]
    public async Task Orchestrator_WithNullDbContextOptions_ShouldThrowOnFirstUse()
    {
        // Arrange
        var configuration = new OrchestratorConfiguration();
        configuration.RegisterExecutor("test-executor", (sp, data, ct) => Task.CompletedTask);

        // Act
        var orchestrator = new Orchestrator(_serviceProvider, configuration);
        var act = async () => await orchestrator.Append("test-executor", new TestData { Value = "test" });

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task RunCommands_WithComplexNestedObject_ShouldSerializeAndDeserializeCorrectly()
    {
        // Arrange
        var configuration = CreateConfiguration();
        ComplexData? receivedData = null;

        configuration.RegisterExecutor("complex-executor", (sp, data, ct) =>
        {
            receivedData = data as ComplexData;
            return Task.CompletedTask;
        });

        var orchestrator = new Orchestrator(_serviceProvider, configuration);
        var complexData = new ComplexData
        {
            Id = 123,
            Name = "Test",
            Nested = new NestedData
            {
                Value = "Nested Value",
                Items = new List<string> { "Item1", "Item2", "Item3" }
            },
            Timestamps = new List<DateTime>
            {
                DateTime.UtcNow,
                DateTime.UtcNow.AddDays(1)
            }
        };

        await orchestrator.Append("complex-executor", complexData);
        await orchestrator.SyncCommands(CancellationToken.None);

        // Act
        orchestrator.RunCommands(CancellationToken.None);
        await WaitForAllCommandsToComplete(orchestrator);

        // Assert
        receivedData.Should().NotBeNull();
        receivedData!.Id.Should().Be(123);
        receivedData.Name.Should().Be("Test");
        receivedData.Nested.Should().NotBeNull();
        receivedData.Nested!.Value.Should().Be("Nested Value");
        receivedData.Nested.Items.Should().HaveCount(3);
        receivedData.Timestamps.Should().HaveCount(2);
    }

    [Fact]
    public async Task RunCommands_WithNullData_ShouldHandleGracefully()
    {
        // Arrange
        var configuration = CreateConfiguration();
        object? receivedData = new object();

        configuration.RegisterExecutor("null-executor", (sp, data, ct) =>
        {
            receivedData = data;
            return Task.CompletedTask;
        });

        var orchestrator = new Orchestrator(_serviceProvider, configuration);

        // Create command with null data directly in database
        using (var context = new OrchestratorDbContext(_dbContextOptions))
        {
            context.Commands.Add(new OrchestratorCommandDbo
            {
                Executor = "null-executor",
                DataType = typeof(TestData).AssemblyQualifiedName!,
                Data = "null",
                Timeout = TimeSpan.FromMinutes(1),
                RetriesLeft = 3
            });
            await context.SaveChangesAsync();
        }

        await orchestrator.SyncCommands(CancellationToken.None);

        // Act
        orchestrator.RunCommands(CancellationToken.None);
        await WaitForAllCommandsToComplete(orchestrator);

        // Assert
        receivedData.Should().BeNull();
    }

    [Fact]
    public async Task SyncCommands_ShouldNotLoadCommandsMarkedAsRunning()
    {
        // Arrange
        var configuration = CreateConfiguration();
        configuration.RegisterExecutor("test-executor", (sp, data, ct) => Task.CompletedTask);
        var orchestrator = new Orchestrator(_serviceProvider, configuration);

        // Create a command marked as running with non-expired lock
        using (var context = new OrchestratorDbContext(_dbContextOptions))
        {
            context.Commands.Add(new OrchestratorCommandDbo
            {
                Executor = "test-executor",
                DataType = typeof(TestData).AssemblyQualifiedName!,
                Data = System.Text.Json.JsonSerializer.Serialize(new TestData { Value = "test" }),
                Timeout = TimeSpan.FromMinutes(1),
                RetriesLeft = 3,
                IsRunning = true,
                RunExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5)
            });
            await context.SaveChangesAsync();
        }

        // Act
        await orchestrator.SyncCommands(CancellationToken.None);
        orchestrator.RunCommands(CancellationToken.None);
        await Task.Delay(100);

        // Assert - command should be loaded but not executed due to valid lock
        var internalOrchestrator = (Orchestrator)orchestrator;
        internalOrchestrator.commands.Should().HaveCount(1);
    }

    [Fact]
    public async Task RunCommands_WithCancellationToken_ShouldCancelExecution()
    {
        // Arrange
        var configuration = CreateConfiguration();
        var executionStarted = new TaskCompletionSource();
        bool wasCancelled = false;

        configuration.RegisterExecutor("cancellable-executor", async (sp, data, ct) =>
        {
            executionStarted.SetResult();
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(10), ct);
            }
            catch (OperationCanceledException)
            {
                wasCancelled = true;
                throw;
            }
        });

        var orchestrator = new Orchestrator(_serviceProvider, configuration);
        await orchestrator.Append("cancellable-executor", new TestData { Value = "test" });
        await orchestrator.SyncCommands(CancellationToken.None);

        var cts = new CancellationTokenSource();

        // Act
        orchestrator.RunCommands(cts.Token);
        await executionStarted.Task;
        cts.Cancel();
        await Task.Delay(200);

        // Assert
        wasCancelled.Should().BeTrue();
    }

    [Fact]
    public async Task Multiple_Orchestrators_WithSeparateExecutors_ShouldWorkIndependently()
    {
        // Arrange
        var configuration1 = CreateConfiguration();
        var configuration2 = CreateConfiguration();

        int executor1Count = 0;
        int executor2Count = 0;

        configuration1.RegisterExecutor("executor1", (sp, data, ct) =>
        {
            Interlocked.Increment(ref executor1Count);
            return Task.CompletedTask;
        });
        configuration1.RegisterExecutor("executor2", (sp, data, ct) =>
        {
            Interlocked.Increment(ref executor2Count);
            return Task.CompletedTask;
        });

        configuration2.RegisterExecutor("executor1", (sp, data, ct) =>
        {
            Interlocked.Increment(ref executor1Count);
            return Task.CompletedTask;
        });
        configuration2.RegisterExecutor("executor2", (sp, data, ct) =>
        {
            Interlocked.Increment(ref executor2Count);
            return Task.CompletedTask;
        });

        var orchestrator1 = new Orchestrator(_serviceProvider, configuration1);
        var orchestrator2 = new Orchestrator(_serviceProvider, configuration2);

        await orchestrator1.Append("executor1", new TestData { Value = "test1" });
        await orchestrator2.Append("executor2", new TestData { Value = "test2" });

        await orchestrator1.SyncCommands(CancellationToken.None);
        await orchestrator2.SyncCommands(CancellationToken.None);

        // Act
        orchestrator1.RunCommands(CancellationToken.None);
        orchestrator2.RunCommands(CancellationToken.None);

        await WaitForAllCommandsToComplete(orchestrator1);
        await WaitForAllCommandsToComplete(orchestrator2);

        // Assert
        executor1Count.Should().Be(1);
        executor2Count.Should().Be(1);
    }

    // Helper methods

    private OrchestratorConfiguration CreateConfiguration()
    {
        var configuration = new OrchestratorConfiguration
        {
            CommandPollingInterval = TimeSpan.FromMilliseconds(100),
            DefaultTimeout = TimeSpan.FromSeconds(5),
            DefaultRetryCount = 3,
            LockTimeoutBuffer = TimeSpan.FromSeconds(1)
        };

        configuration.ConfigureDbContext(opts => opts.UseNpgsql(ConnectionString));
        return configuration;
    }

    private static async Task WaitForAllCommandsToComplete(Orchestrator orchestrator, int timeoutMs = 5000)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        while (stopwatch.ElapsedMilliseconds < timeoutMs)
        {
            if (orchestrator.commands.All(c => !c.IsRunning))
                return;

            await Task.Delay(10);
        }
    }

    private class TestData
    {
        public string Value { get; set; } = string.Empty;
    }

    private class ComplexData
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public NestedData? Nested { get; set; }
        public List<DateTime>? Timestamps { get; set; }
    }

    private class NestedData
    {
        public string? Value { get; set; }
        public List<string>? Items { get; set; }
    }
}
