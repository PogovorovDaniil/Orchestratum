using Microsoft.Extensions.DependencyInjection;
using Orchestratum.Contract;

namespace Orchestratum.Services;

internal class CommandExecutor(IServiceProvider serviceProvider, OrchServiceConfiguration configuration, OrchDataService dataService)
{
    private Task? runningTask;

    public CommandExecutor Run(IOrchCommand command, CancellationToken cancellationToken)
    {
        runningTask = RunAsync(command, cancellationToken);
        return this;
    }

    public bool IsFinished => runningTask?.IsCompleted ?? false;

    private async Task RunAsync(IOrchCommand command, CancellationToken cancellationToken = default)
    {
        using var runCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        try
        {
            runCts.CancelAfter(command.Timeout);
            _ = ExtendAsync(command, runCts.Token);
            IOrchResult result = await ExecuteAsync(command, runCts.Token);
            switch (result.Status)
            {
                case OrchResultStatus.Success:
                    await dataService.CompleteCommandAsync(command.Id, result.Output, command.OnSuccess(result.Output), runCts.Token);
                    break;
                case OrchResultStatus.Cancelled:
                    await dataService.CancelCommandAsync(command.Id, command.OnCancellation(), runCts.Token);
                    break;
                default:
                    await dataService.FailCommandAsync(command.Id, command.OnFailure(), runCts.Token);
                    break;
            }
        }
        finally
        {
            runCts.Cancel();
        }
    }

    private async Task<IOrchResult> ExecuteAsync(IOrchCommand command, CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        try
        {
            IOrchCommandHandler? commandHandler = (IOrchCommandHandler?)scope.ServiceProvider
                .GetKeyedService(typeof(IOrchCommandHandler), command.Name);

            if (commandHandler is null) return new EmptyOrchResult(OrchResultStatus.NotFound);
            return await commandHandler.Execute(command, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return new EmptyOrchResult(OrchResultStatus.TimedOut);
        }
        catch (Exception)
        {
            return new EmptyOrchResult(OrchResultStatus.Failed);
        }
    }

    private async Task ExtendAsync(IOrchCommand command, CancellationToken cancellationToken)
    {
        try
        {
            do
            {
                await Task.Delay(configuration.LockTimeoutBuffer / 2, cancellationToken);
            } while (await dataService.ExtendCommandLockAsync(command.Id, configuration.LockTimeoutBuffer, cancellationToken));
        }
        catch (OperationCanceledException) { }
    }
}