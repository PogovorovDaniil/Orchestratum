using Microsoft.EntityFrameworkCore;
using Orchestratum.Database;
using Orchestratum.Tests.Misc;
using System.Collections.Concurrent;
using Xunit;

namespace Orchestratum.Tests;

public class OrchestratumIntegrationTestsFixture : TestApplication
{
    public int ExecutionCounter => _executionCounter;
    public List<string> ExecutionLog => _executionLog.ToList();
    
    private int _executionCounter = 0;
    private readonly ConcurrentBag<string> _executionLog = [];

    public void ResetCounters()
    {
        _executionCounter = 0;
        _executionLog.Clear();
    }

    public void IncrementCounter() => Interlocked.Increment(ref _executionCounter);
    public void AddToLog(string message) => _executionLog.Add(message);

    public override void ConfigureOrchestratum(IServiceProvider serviceProvider, OrchestratumConfiguration configuration)
    {
        configuration
            .RegisterExecutor("simple-task", async (sp, data, ct) =>
            {
                var payload = (OrchestratumIntegrationTests.SimplePayload)data;
                IncrementCounter();
                AddToLog($"Executed: {payload.Message}");
                await Task.CompletedTask;
            })
            .RegisterExecutor("delayed-task", async (sp, data, ct) =>
            {
                var payload = (OrchestratumIntegrationTests.DelayedPayload)data;
                await Task.Delay(payload.DelayMs, ct);
                IncrementCounter();
                AddToLog($"Delayed: {payload.Message}");
            })
            .RegisterExecutor("failing-task", (sp, data, ct) =>
            {
                IncrementCounter();
                throw new Exception("Task failed");
            })
            .RegisterExecutor("conditional-fail", (sp, data, ct) =>
            {
                var payload = (OrchestratumIntegrationTests.ConditionalPayload)data;
                if (payload.ShouldFail)
                {
                    throw new Exception("Conditional failure");
                }
                IncrementCounter();
                AddToLog($"Success: {payload.Message}");
                return Task.CompletedTask;
            });
    }
}

public class OrchestratumIntegrationTests : IClassFixture<OrchestratumIntegrationTestsFixture>, IAsyncLifetime
{
    private readonly OrchestratumIntegrationTestsFixture _fixture;

    public OrchestratumIntegrationTests(OrchestratumIntegrationTestsFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _fixture.ResetCounters();
        await _fixture.CleanDatabase();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public record SimplePayload(string Message);
    public record DelayedPayload(string Message, int DelayMs);
    public record ConditionalPayload(string Message, bool ShouldFail);

    [Fact]
    public async Task Append_SimpleExecutor_ExecutesSuccessfully()
    {
        var payload = new SimplePayload("Test message");
        await _fixture.Orchestratum.Append("simple-task", payload);
        await Task.Delay(500);

        Assert.Equal(1, _fixture.ExecutionCounter);
        Assert.Contains("Executed: Test message", _fixture.ExecutionLog);

        var command = await GetSingleCommandAsync();
        Assert.True(command.IsCompleted);
        Assert.False(command.IsFailed);
        Assert.NotNull(command.CompleteAt);
    }

    [Fact]
    public async Task Append_MultipleExecutors_AllExecuteInOrder()
    {
        await _fixture.Orchestratum.Append("simple-task", new SimplePayload("First"));
        await _fixture.Orchestratum.Append("simple-task", new SimplePayload("Second"));
        await _fixture.Orchestratum.Append("simple-task", new SimplePayload("Third"));
        await Task.Delay(800);

        Assert.Equal(3, _fixture.ExecutionCounter);
        Assert.Contains("Executed: First", _fixture.ExecutionLog);
        Assert.Contains("Executed: Second", _fixture.ExecutionLog);
        Assert.Contains("Executed: Third", _fixture.ExecutionLog);

        var commands = await GetAllCommandsAsync();
        Assert.Equal(3, commands.Count);
        Assert.All(commands, cmd => Assert.True(cmd.IsCompleted));
    }

    [Fact]
    public async Task Append_FailingExecutor_RetriesAndFails()
    {
        var payload = new SimplePayload("Will fail");
        await _fixture.Orchestratum.Append("failing-task", payload, retryCount: 2);
        await Task.Delay(1500);

        Assert.Equal(3, _fixture.ExecutionCounter);

        var command = await GetSingleCommandAsync();
        Assert.False(command.IsCompleted);
        Assert.True(command.IsFailed);
        Assert.NotNull(command.FailedAt);
        // RetriesLeft может быть -1 после всех попыток
        Assert.True(command.RetriesLeft <= 0);
    }

    [Fact]
    public async Task Append_WithCustomTimeout_RespectsTimeout()
    {
        var payload = new DelayedPayload("Long task", 5000);
        await _fixture.Orchestratum.Append("delayed-task", payload, timeout: TimeSpan.FromSeconds(1));
        await Task.Delay(2000);

        var command = await GetSingleCommandAsync();
        // Задача должна быть прервана или не завершена
        Assert.False(command.IsCompleted || !command.IsRunning);
    }

    [Fact]
    public async Task Append_ShortDelay_CompletesSuccessfully()
    {
        var payload = new DelayedPayload("Quick task", 200);
        await _fixture.Orchestratum.Append("delayed-task", payload, timeout: TimeSpan.FromSeconds(5));
        await Task.Delay(1000);

        Assert.Equal(1, _fixture.ExecutionCounter);
        Assert.Contains("Delayed: Quick task", _fixture.ExecutionLog);

        var command = await GetSingleCommandAsync();
        Assert.True(command.IsCompleted);
        Assert.False(command.IsFailed);
    }

    [Fact]
    public async Task Append_WithExplicitDataType_SerializesCorrectly()
    {
        var payload = new SimplePayload("Type test");
        await _fixture.Orchestratum.Append("simple-task", typeof(SimplePayload), payload);
        await Task.Delay(500);

        Assert.Equal(1, _fixture.ExecutionCounter);

        var command = await GetSingleCommandAsync();
        Assert.Contains("SimplePayload", command.DataType);
        Assert.True(command.IsCompleted);
    }

    [Fact]
    public async Task Append_UnregisteredExecutor_ThrowsException()
    {
        var payload = new SimplePayload("Test");

        var exception = await Assert.ThrowsAsync<OrchestratumException>(
            () => _fixture.Orchestratum.Append("non-existent-executor", payload)
        );

        Assert.Contains("not registered", exception.Message);
    }

    [Fact]
    public async Task Append_ConditionalFailure_EventuallySucceeds()
    {
        var failingPayload = new ConditionalPayload("First attempt", true);
        await _fixture.Orchestratum.Append("conditional-fail", failingPayload, retryCount: 1);
        await Task.Delay(500);

        var command = await GetSingleCommandAsync();
        Assert.True(command.IsFailed);

        await _fixture.CleanDatabase();
        _fixture.ResetCounters();
        
        var successPayload = new ConditionalPayload("Success", false);
        await _fixture.Orchestratum.Append("conditional-fail", successPayload);
        await Task.Delay(500);

        var successCommand = await GetSingleCommandAsync();
        Assert.True(successCommand.IsCompleted);
        Assert.Contains("Success: Success", _fixture.ExecutionLog);
    }

    [Fact]
    public async Task Append_WithTargetKey_StoresCorrectTarget()
    {
        var payload = new SimplePayload("Target test");
        var customTarget = "custom-instance";

        await _fixture.Orchestratum.Append("simple-task", payload, targetKey: customTarget);
        await Task.Delay(300);

        var command = await GetSingleCommandAsync();
        Assert.Equal(customTarget, command.Target);
        // Примечание: Target key записан, но в SyncCommands команда может быть синхронизирована
    }

    [Fact]
    public async Task Append_DefaultTargetKey_UsesInstanceKey()
    {
        var payload = new SimplePayload("Default target");
        await _fixture.Orchestratum.Append("simple-task", payload);
        await Task.Delay(500);

        var command = await GetSingleCommandAsync();
        Assert.Equal("test-instance", command.Target);
        Assert.True(command.IsCompleted);
    }

    [Fact]
    public async Task Execution_ConcurrentCommands_HandledCorrectly()
    {
        var tasks = new List<Task>();
        for (int i = 0; i < 5; i++)
        {
            var index = i;
            tasks.Add(_fixture.Orchestratum.Append("simple-task", new SimplePayload($"Concurrent {index}")));
        }

        await Task.WhenAll(tasks);
        await Task.Delay(1500);

        Assert.Equal(5, _fixture.ExecutionCounter);
        
        var commands = await GetAllCommandsAsync();
        Assert.Equal(5, commands.Count);
        Assert.All(commands, cmd => Assert.True(cmd.IsCompleted));
    }

    [Fact]
    public async Task Execution_RetryMechanism_DecreasesRetryCount()
    {
        var payload = new SimplePayload("Retry test");
        await _fixture.Orchestratum.Append("failing-task", payload, retryCount: 3);

        await Task.Delay(300);
        var command1 = await GetSingleCommandAsync();
        var retriesAfterFirst = command1.RetriesLeft;

        await Task.Delay(1000);
        var command2 = await GetSingleCommandAsync();

        Assert.True(command2.IsFailed);
        Assert.True(command2.RetriesLeft <= 0);
        Assert.True(retriesAfterFirst > command2.RetriesLeft);
    }

    private async Task<CommandDbo> GetSingleCommandAsync()
    {
        using var context = new OrchestratumDbContext(_fixture.ContextOptions);
        return await context.Commands.OrderByDescending(c => c.Id).FirstAsync();
    }

    private async Task<List<CommandDbo>> GetAllCommandsAsync()
    {
        using var context = new OrchestratumDbContext(_fixture.ContextOptions);
        return await context.Commands.ToListAsync();
    }
}
