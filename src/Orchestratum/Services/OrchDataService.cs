using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Orchestratum.Contract;
using Orchestratum.Database;
using System.Text.Json;

namespace Orchestratum.Services;

/// <summary>
/// Service for managing database operations with OrchestratumDbContext.
/// </summary>
internal class OrchDataService(DbContextOptions<OrchDbContext> contextOptions, OrchServiceConfiguration configuration)
{
    private OrchDbContext CreateContext() => new(contextOptions, configuration);

    private async Task AddCommandsAsync(OrchDbContext context, IEnumerable<IOrchCommand> commands, CancellationToken cancellationToken = default)
    {
        foreach (var command in commands)
        {
            var now = DateTimeOffset.UtcNow;
            var name = CommandNameHelper.GetCommandName(command.GetType());
            var dataSerialized = command.Input is null ? null : JsonSerializer.Serialize(command.Input, command.InputType);
            context.Add(new OrchCommandDbo()
            {
                Id = command.Id,
                Name = name,
                Target = command.Target ?? configuration.InstanceKey,
                Input = dataSerialized,
                RetriesLeft = command.RetryCount,
                Timeout = command.Timeout,
                ScheduledAt = now + command.Delay,
            });
        }
        await context.SaveChangesAsync(cancellationToken);
    }

    public Task AddCommandAsync(IOrchCommand command, CancellationToken cancellationToken = default) => AddCommandsAsync(CreateContext(), [command], cancellationToken);

    public async Task<Guid> GetPendingCommandAsync(string target, CancellationToken cancellationToken = default)
    {
        using var context = CreateContext();
        var now = DateTimeOffset.UtcNow;
        return await context.Commands
            .Where(c => c.Target == target && c.ScheduledAt <= now &&
                (!c.IsRunning || c.RunExpiresAt < now) &&
                !c.IsCompleted &&
                !c.IsFailed &&
                !c.IsCanceled)
            .Select(c => c.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<OrchCommandDbo?> RunCommandAsync(Guid commandId, TimeSpan lockTimeout, CancellationToken cancellationToken = default)
    {
        using var context = CreateContext();
        var now = DateTimeOffset.UtcNow;
        var runExpiresAt = now + lockTimeout;
        var updated = await context.Commands
            .Where(c => c.Id == commandId &&
                (!c.IsRunning || c.RunExpiresAt < now) &&
                !c.IsCompleted &&
                !c.IsCanceled &&
                !c.IsFailed)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.IsRunning, true)
                .SetProperty(c => c.RunningAt, now)
                .SetProperty(c => c.RunExpiresAt, c => runExpiresAt), cancellationToken);

        if (updated == 0) return null;

        var command = await context.Commands
            .AsNoTracking()
            .FirstAsync(c => c.Id == commandId, cancellationToken);

        return command;
    }

    public async Task<bool> ExtendCommandLockAsync(Guid commandId, TimeSpan lockTimeout, CancellationToken cancellationToken = default)
    {
        using var context = CreateContext();

        var now = DateTimeOffset.UtcNow;
        var runExpiresAt = now + lockTimeout;
        var updated = await context.Commands
            .Where(c => c.Id == commandId && c.IsRunning && c.RunExpiresAt > now)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.RunExpiresAt, c => runExpiresAt), cancellationToken);

        return updated > 0;
    }

    public async Task<bool> CompleteCommandAsync(Guid commandId, object? output, IEnumerable<IOrchCommand> scheduledCommands, CancellationToken cancellationToken = default)
    {
        var outputSerialized = output is null ? null : JsonSerializer.Serialize(output, output!.GetType());
        using var context = CreateContext();
        using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var now = DateTimeOffset.UtcNow;
            var updated = await context.Commands
                .Where(c => c.Id == commandId && c.IsRunning && !c.IsCompleted)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(c => c.IsRunning, false)
                    .SetProperty(c => c.RunExpiresAt, (DateTimeOffset?)null)
                    .SetProperty(c => c.IsCompleted, true)
                    .SetProperty(c => c.Output, outputSerialized)
                    .SetProperty(c => c.CompletedAt, now), cancellationToken);

            if (updated > 0) await AddCommandsAsync(context, scheduledCommands, cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return updated > 0;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<bool> CancelCommandAsync(Guid commandId, IEnumerable<IOrchCommand> scheduledCommands, CancellationToken cancellationToken = default)
    {
        using var context = CreateContext();
        using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var now = DateTimeOffset.UtcNow;
            var updated = await context.Commands
                .Where(c => c.Id == commandId && c.IsRunning && !c.IsCanceled)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(c => c.IsRunning, false)
                    .SetProperty(c => c.RunExpiresAt, (DateTimeOffset?)null)
                    .SetProperty(c => c.IsCanceled, true)
                    .SetProperty(c => c.CanceledAt, now), cancellationToken);


            if (updated > 0) await AddCommandsAsync(context, scheduledCommands, cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return updated > 0;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<bool> FailCommandAsync(Guid commandId, IEnumerable<IOrchCommand> scheduledCommands, CancellationToken cancellationToken = default)
    {
        using var context = CreateContext();
        var now = DateTimeOffset.UtcNow;
        using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await context.Commands
                .Where(c => c.Id == commandId && c.IsRunning && !c.IsCompleted)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(c => c.IsRunning, false)
                    .SetProperty(c => c.RetriesLeft, c => c.RetriesLeft - 1)
                    .SetProperty(c => c.RunExpiresAt, (DateTimeOffset?)null), cancellationToken);

            var updated = await context.Commands
                .Where(c => c.Id == commandId && !c.IsFailed && c.RetriesLeft == -1)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(c => c.IsFailed, true)
                    .SetProperty(c => c.FailedAt, now), cancellationToken);

            if (updated > 0) await AddCommandsAsync(context, scheduledCommands, cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return updated > 0;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
