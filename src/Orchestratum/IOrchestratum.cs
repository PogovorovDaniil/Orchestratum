using Orchestratum.Contract;

namespace Orchestratum;

/// <summary>
/// Provides orchestration functionality for managing command execution.
/// </summary>
public interface IOrchestratum
{
    /// <summary>
    /// Pushes a command into the orchestration queue for execution.
    /// </summary>
    /// <param name="command">The command to be executed.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task Push(IOrchCommand command);
}
