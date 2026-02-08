namespace Orchestratum;

/// <summary>
/// Delegate that defines the signature for task executor functions.
/// </summary>
/// <param name="serviceProvider">The service provider for dependency injection.</param>
/// <param name="data">The data payload for the task.</param>
/// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
/// <returns>A task representing the asynchronous operation.</returns>
public delegate Task ExecutorDelegate(IServiceProvider serviceProvider, object data, CancellationToken cancellationToken);

/// <summary>
/// Orchestratum for managing background tasks with persistence, retries, and timeouts.
/// </summary>
public interface IOrchestratum
{
    /// <summary>
    /// Appends a new command to the orchestrator queue.
    /// </summary>
    /// <param name="executorKey">Key identifying the executor responsible for processing the command.</param>
    /// <param name="data">Command payload.</param>
    /// <param name="targetKey">Target instance key for command execution; if null, the current instance key is used.</param>
    /// <param name="timeout">Optional execution timeout; defaults to <see cref="DefaultTimeout"/>.</param>
    /// <param name="retryCount">Optional retry count; defaults to <see cref="DefaultRetryCount"/>.</param>
    Task Append(string executorKey, object data, string? targetKey = null, TimeSpan? timeout = null, int? retryCount = null);

    /// <summary>
    /// Appends a new command to the orchestrator queue with an explicit payload type.
    /// </summary>
    /// <param name="executorKey">Key identifying the executor responsible for processing the command.</param>
    /// <param name="dataType">Explicit payload type used for serialization.</param>
    /// <param name="data">Command payload.</param>
    /// <param name="targetKey">Target instance key for command execution; if null, the current instance key is used.</param>
    /// <param name="timeout">Optional execution timeout; defaults to <see cref="DefaultTimeout"/>.</param>
    /// <param name="retryCount">Optional retry count; defaults to <see cref="DefaultRetryCount"/>.</param>
    Task Append(string executorKey, Type dataType, object data, string? targetKey = null, TimeSpan? timeout = null, int? retryCount = null);

    internal Task SyncCommands(CancellationToken cancellationToken);
    internal void RunCommands(CancellationToken cancellationToken);
    internal Task WaitPollingInterval(CancellationToken cancellationToken);
}
