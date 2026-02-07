using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Orchestratum.Database;
using System.Text.Json;

namespace Orchestratum.Services;

/// <summary>
/// Main orchestrator implementation that manages background task execution with persistence and retries.
/// </summary>
public class Orchestrator : IOrchestrator
{
    internal readonly IServiceProvider serviceProvider;
    internal readonly DbContextOptions<OrchestratorDbContext> contextOptions;
    internal readonly ILogger? logger;

    internal readonly TimeSpan lockTimeoutBuffer;
    private readonly TimeSpan defaultTimeout;
    private readonly int defaultRetryCount;

    private readonly TimeSpan commandPollingInterval;
    internal readonly Dictionary<string, ExecutorDelegate> executors = [];
    internal readonly HashSet<OrchestratorCommand> commands = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="Orchestrator"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider for dependency injection.</param>
    /// <param name="configuration">The orchestrator configuration.</param>
    public Orchestrator(IServiceProvider serviceProvider, OrchestratorConfiguration configuration)
    {
        this.serviceProvider = serviceProvider;
        logger = serviceProvider.GetService<ILogger<IOrchestrator>>();

        foreach (var executor in configuration.storedExecutors)
        {
            if (executors.ContainsKey(executor.Key)) throw new OrchestratorException(
                $"Cannot register command '{executor.Key}': a command with this type already exists."
            );
            executors.Add(executor.Key, executor.Value);
        }

        lockTimeoutBuffer = configuration.LockTimeoutBuffer;
        defaultTimeout = configuration.DefaultTimeout;
        defaultRetryCount = configuration.DefaultRetryCount;
        commandPollingInterval = configuration.CommandPollingInterval;
        contextOptions = configuration.contextOptions;
    }

    /// <inheritdoc/>
    public Task Append(string executorKey, object data, TimeSpan? timeout = null, int? retryCount = null) =>
        Append(executorKey, data.GetType(), data, timeout, retryCount);

    /// <inheritdoc/>
    public async Task Append(string executorKey, Type dataType, object data, TimeSpan? timeout = null, int? retryCount = null)
    {
        if (!executors.ContainsKey(executorKey)) throw new OrchestratorException(
                $"Cannot append command: executor with type '{executorKey}' is not registered."
            );

        var dataSerialized = JsonSerializer.Serialize(data, dataType);
        var commandDbo = new OrchestratorCommandDbo()
        {
            Executor = executorKey,
            DataType = dataType.AssemblyQualifiedName!,
            Data = dataSerialized,
            RetriesLeft = retryCount ?? defaultRetryCount,
            Timeout = timeout ?? defaultTimeout
        };

        using (var context = new OrchestratorDbContext(contextOptions))
        {
            context.Add(commandDbo);
            await context.SaveChangesAsync();
        }
        var command = new OrchestratorCommand(this, commandDbo.Id);
        commands.Add(command);

        lock (waitPollingCts)
        {
            if (!waitPollingCts.IsCancellationRequested)
                waitPollingCts.Cancel();
        }
    }

    /// <summary>
    /// Synchronizes commands from the database into memory.
    /// Loads any commands that are not completed or failed.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task SyncCommands(CancellationToken cancellationToken)
    {
        using var context = new OrchestratorDbContext(contextOptions);
        var commandDbos = context.Set<OrchestratorCommandDbo>();
        var actualCommands = await commandDbos.Where(c => !c.IsCompleted && !c.IsFailed).Select(c => c.Id).ToListAsync(cancellationToken);
        foreach (var commandId in actualCommands)
        {
            if (commands.Any(c => c.CommandId == commandId)) continue;
            commands.Add(new OrchestratorCommand(this, commandId));
        }
    }

    /// <summary>
    /// Runs all pending commands that are not currently running.
    /// Cleans up completed and failed commands.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public void RunCommands(CancellationToken cancellationToken)
    {
        var commands = this.commands.ToList();
        foreach (var command in commands)
        {
            if (command.IsCompleted || command.IsFailed)
            {
                this.commands.Remove(command);
                command.Dispose();
            }
            if (command.IsRunning) continue;
            command.Run(cancellationToken);
        }
    }

    private CancellationTokenSource waitPollingCts = new();

    /// <summary>
    /// Waits for the configured polling interval before checking for new commands again.
    /// Can be interrupted by adding new commands or cancellation.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task WaitPollingInterval(CancellationToken cancellationToken)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, waitPollingCts.Token);
        try
        {
            await Task.Delay(commandPollingInterval, cts.Token);
        }
        catch (OperationCanceledException ex) when (ex.CancellationToken == cancellationToken)
        {
            throw;
        }
        catch (OperationCanceledException) { }
        finally
        {
            lock (waitPollingCts) waitPollingCts = new();
        }
    }
}
