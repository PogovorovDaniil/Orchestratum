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
/// Orchestrator for managing background tasks with persistence, retries, and timeouts.
/// </summary>
public interface IOrchestrator
{
    /// <summary>
    /// Appends a new task to the orchestrator queue.
    /// </summary>
    /// <param name="executorKey">The key identifying the executor to handle this task.</param>
    /// <param name="data">The data payload for the task.</param>
    /// <param name="timeout">Optional timeout for task execution. If not specified, uses DefaultTimeout.</param>
    /// <param name="retryCount">Optional number of retry attempts. If not specified, uses DefaultRetryCount.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task Append(string executorKey, object data, TimeSpan? timeout = null, int? retryCount = null);

    /// <summary>
    /// Appends a new task to the orchestrator queue with explicit type information.
    /// </summary>
    /// <param name="executorKey">The key identifying the executor to handle this task.</param>
    /// <param name="dataType">The type of the data payload for serialization.</param>
    /// <param name="data">The data payload for the task.</param>
    /// <param name="timeout">Optional timeout for task execution. If not specified, uses DefaultTimeout.</param>
    /// <param name="retryCount">Optional number of retry attempts. If not specified, uses DefaultRetryCount.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task Append(string executorKey, Type dataType, object data, TimeSpan? timeout = null, int? retryCount = null);

    internal Task SyncCommands(CancellationToken cancellationToken);
    internal void RunCommands(CancellationToken cancellationToken);
    internal Task WaitPollingInterval(CancellationToken cancellationToken);
}
