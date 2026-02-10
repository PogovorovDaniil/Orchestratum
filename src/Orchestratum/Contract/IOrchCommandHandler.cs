namespace Orchestratum.Contract;

/// <summary>
/// Base interface for command handlers.
/// </summary>
public interface IOrchCommandHandler
{
    internal Task<IOrchResult> Execute(IOrchCommand command, CancellationToken cancellationToken);
}

/// <summary>
/// Defines a handler for executing commands of a specific type.
/// </summary>
/// <typeparam name="TCommand">The type of command this handler can execute.</typeparam>
public interface IOrchCommandHandler<TCommand> : IOrchCommandHandler
    where TCommand : IOrchCommand
{
    async Task<IOrchResult> IOrchCommandHandler.Execute(IOrchCommand command, CancellationToken cancellationToken) => await Execute((TCommand)command!, cancellationToken);

    /// <summary>
    /// Executes the specified command.
    /// </summary>
    /// <param name="command">The command to execute.</param>
    /// <param name="cancellationToken">The cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous operation, containing the execution result.</returns>
    Task<IOrchResult<TCommand>> Execute(TCommand command, CancellationToken cancellationToken);
}
