using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Orchestratum.Database;
using System.Diagnostics;
using System.Text.Json;

namespace Orchestratum.Services;

internal class CommandHelper(Orchestratum orchestrator, Guid commandId) : IDisposable
{
    internal Guid CommandId { get; init; } = commandId;
    private readonly CancellationTokenSource disposeCts = new CancellationTokenSource();
    private Task? runningTask;

    public bool IsRunning => !(runningTask?.IsCompleted ?? true);
    public bool IsCompleted { get; private set; } = false;
    public bool IsFailed { get; private set; } = false;

    public void Run(CancellationToken cancellationToken = default)
    {
        if (IsRunning)
            throw new OrchestratumException("Cannot start command execution because it is already running.");
        runningTask = RunAsync(cancellationToken);
    }

    public void Dispose()
    {
        disposeCts.Cancel();
    }

    private async Task RunAsync(CancellationToken cancellationToken = default)
    {
        var runCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, disposeCts.Token);

        using var context = new OrchestratumDbContext(orchestrator.contextOptions);
        try
        {
            var command = await RunLock(context.Commands, orchestrator.lockTimeoutBuffer, runCts.Token);
            if (command is null) return;

            runCts.CancelAfter(command.Timeout);

            Type dataType = Type.GetType(command.DataType) ?? throw new OrchestratumException(
                    $"Failed to resolve type '{command.DataType}'. Ensure the type name is correct and the assembly is loaded."
                );
            var data = JsonSerializer.Deserialize(command.Data, dataType)!;

            var executor = orchestrator.executors[command.Executor];
            await Execute(executor, data, command.Executor, runCts.Token);

            await Complete(context.Commands, runCts.Token);
            IsCompleted = true;
        }
        catch (OperationCanceledException ex)
        {
            if (disposeCts.IsCancellationRequested)
            {
                orchestrator.logger?.LogWarning(ex,
                    "Command {CommandId} was cancelled due to disposal.", CommandId);
            }
            else if (cancellationToken.IsCancellationRequested)
            {
                orchestrator.logger?.LogWarning(ex,
                    "Command {CommandId} was cancelled externally.", CommandId);
            }
            else
            {
                orchestrator.logger?.LogWarning(ex,
                    "Command {CommandId} execution timed out.",
                    CommandId);
            }
        }
        catch (Exception ex)
        {
            orchestrator.logger?.LogError(ex, "Error while running command {CommandId}.", CommandId);
        }
        finally
        {
            if (!IsCompleted) await Fail(context.Commands);
            if (IsCompleted || IsFailed)
            {
                orchestrator.commands.Remove(this);
                Dispose();
            }
        }
    }

    private async ValueTask Execute(ExecutorDelegate executor, object data, string executorKey, CancellationToken cancellationToken)
    {
        orchestrator.logger?.LogInformation(
            "Executing command {CommandId} using executor '{Executor}'.",
            CommandId,
            executorKey);
        var stopwatch = Stopwatch.StartNew();
        await executor(orchestrator.serviceProvider, data, cancellationToken);
        stopwatch.Stop();
        orchestrator.logger?.LogInformation(
            "Command {CommandId} executed successfully in {ElapsedMilliseconds} ms.",
            CommandId,
            stopwatch.ElapsedMilliseconds
        );
    }

    private async ValueTask<CommandDbo?> RunLock(DbSet<CommandDbo> commandDbos, TimeSpan lockTimeoutBuffer, CancellationToken cancellationToken = default)
    {
        var commandId = CommandId;
        var now = DateTimeOffset.UtcNow;
        var runExpiresAt = now + lockTimeoutBuffer;
        var updated = await commandDbos
            .Where(c => c.Id == commandId && (!c.IsRunning || c.RunExpiresAt < now) && c.RetriesLeft >= 0 && !c.IsCompleted)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.IsRunning, true)
                .SetProperty(c => c.RunExpiresAt, c => runExpiresAt + c.Timeout), cancellationToken);

        if (updated == 0) return null;

        return await commandDbos
            .AsNoTracking()
            .FirstAsync(c => c.Id == commandId);
    }

    private async ValueTask Fail(DbSet<CommandDbo> commandDbos)
    {
        var commandId = CommandId;
        var now = DateTimeOffset.UtcNow;
        await commandDbos
            .Where(c => c.Id == commandId && c.IsRunning && !c.IsCompleted)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.IsRunning, false)
                .SetProperty(c => c.RetriesLeft, c => c.RetriesLeft - 1)
                .SetProperty(c => c.RunExpiresAt, (DateTimeOffset?)null));

        await commandDbos
            .Where(c => c.Id == commandId && !c.IsFailed && c.RetriesLeft == -1)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.IsFailed, true)
                .SetProperty(c => c.FailedAt, now));

        if (await commandDbos.AnyAsync(c => c.Id == CommandId && c.IsFailed))
        {
            IsFailed = true;
            orchestrator.logger?.LogInformation("Command {CommandId} is failed.", CommandId);
        }
    }

    private async ValueTask Complete(DbSet<CommandDbo> commandDbos, CancellationToken cancellationToken = default)
    {
        var commandId = CommandId;
        var now = DateTimeOffset.UtcNow;
        var updated = await commandDbos
            .Where(c => c.Id == commandId && c.IsRunning && !c.IsCompleted)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.IsRunning, false)
                .SetProperty(c => c.RunExpiresAt, (DateTimeOffset?)null)
                .SetProperty(c => c.IsCompleted, true)
                .SetProperty(c => c.CompleteAt, now), cancellationToken);

        if (await commandDbos.AnyAsync(c => c.Id == CommandId && c.IsCompleted))
        {
            IsCompleted = true;
            orchestrator.logger?.LogInformation("Command {CommandId} is already completed.", CommandId);
            return;
        }
    }
}