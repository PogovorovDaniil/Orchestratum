using Microsoft.Extensions.DependencyInjection;
using Orchestratum.Contract;
using System.Text.Json;

namespace Orchestratum.Services;

internal class Orchestratum(IServiceProvider serviceProvider, OrchServiceConfiguration configuration, OrchDataService dataService) : IOrchestratum
{
    private List<CommandExecutor> commandExecutors = [];

    public async Task Push(IOrchCommand command)
    {
        await dataService.AddCommandAsync(command);
        NotifyNewCommands();
    }

    public async Task RunCommands(CancellationToken cancellationToken)
    {
        while (true)
        {
            Guid commandId = await dataService.GetPendingCommandAsync(configuration.InstanceKey, cancellationToken);
            if (commandId == default) break;
            var commandDbo = await dataService.RunCommandAsync(commandId, configuration.LockTimeoutBuffer, cancellationToken);
            if (commandDbo is null) continue;
            var command = serviceProvider.GetKeyedService<IOrchCommand>(commandDbo.Name);
            if (command is null)
            {
                await dataService.FailCommandAsync(commandId, [], cancellationToken);
                continue;
            }
            command.Name = commandDbo.Name;
            command.Id = commandDbo.Id;
            command.Target = commandDbo.Target;
            command.Timeout = commandDbo.Timeout;
            command.Input = commandDbo.Input is null ? null : JsonSerializer.Deserialize(commandDbo.Input, command.InputType);
            var executor = serviceProvider.GetRequiredService<CommandExecutor>();
            lock (commandExecutors)
            {
                commandExecutors.Add(executor.Run(command, cancellationToken));
                if (commandExecutors.Count >= configuration.MaxCommandPull) break;
            }
        }
    }

    public void ClearCommands()
    {
        CommandExecutor[] commands = Array.Empty<CommandExecutor>();
        lock (commandExecutors) commands = commandExecutors.ToArray();
        foreach (var command in commands)
        {
            if (command.IsFinished) commandExecutors.Remove(command);
        }
    }

    private CancellationTokenSource waitPollingCts = new();
    private readonly object waitPollingLock = new();

    public async Task WaitPollingInterval(CancellationToken cancellationToken)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, waitPollingCts.Token);
        try
        {
            await Task.Delay(configuration.CommandPollingInterval, cts.Token);
        }
        catch (OperationCanceledException ex) when (ex.CancellationToken == cancellationToken)
        {
            throw;
        }
        catch (OperationCanceledException) { }
        finally
        {
            lock (waitPollingLock) waitPollingCts = new();
        }
    }

    internal void NotifyNewCommands()
    {
        lock (waitPollingLock)
        {
            if (!waitPollingCts.IsCancellationRequested)
                waitPollingCts.Cancel();
        }
    }
}
