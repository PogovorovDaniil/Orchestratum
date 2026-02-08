using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Orchestratum.Database;
using System.Text.Json;

namespace Orchestratum.Services;

/// <summary>
/// Main orchestratum implementation that manages background task execution with persistence and retries.
/// </summary>
internal class Orchestratum : IOrchestratum
{
    internal readonly IServiceProvider serviceProvider;
    internal readonly DbContextOptions<OrchestratumDbContext> contextOptions;
    internal readonly ILogger? logger;

    internal readonly TimeSpan lockTimeoutBuffer;
    private readonly TimeSpan defaultTimeout;
    private readonly int defaultRetryCount;

    private readonly TimeSpan commandPollingInterval;
    internal readonly Dictionary<string, ExecutorDelegate> executors = [];
    internal readonly HashSet<CommandHelper> commands = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="Orchestratum"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider for dependency injection.</param>
    /// <param name="configuration">The orchestratum configuration.</param>
    public Orchestratum(IServiceProvider serviceProvider, OrchestratumConfiguration configuration)
    {
        this.serviceProvider = serviceProvider;
        logger = serviceProvider.GetService<ILogger<IOrchestratum>>();

        foreach (var executor in configuration.storedExecutors)
        {
            if (executors.ContainsKey(executor.Key)) throw new OrchestratumException(
                $"Cannot register command '{executor.Key}': a command with this type already exists."
            );
            executors.Add(executor.Key, executor.Value);
        }

        lockTimeoutBuffer = configuration.LockTimeoutBuffer;
        defaultTimeout = configuration.DefaultTimeout;
        defaultRetryCount = configuration.DefaultRetryCount;
        commandPollingInterval = configuration.CommandPollingInterval;
        contextOptions = configuration.contextOptions;
        InstanceKey = configuration.InstanceKey;
    }

    public string InstanceKey { get; private set; }

    /// <inheritdoc/>
    public Task Append(string executorKey, object data, string? targetKey = null, TimeSpan? timeout = null, int? retryCount = null) =>
        Append(executorKey, data.GetType(), data, targetKey, timeout, retryCount);

    /// <inheritdoc/>
    public async Task Append(string executorKey, Type dataType, object data, string? targetKey = null, TimeSpan? timeout = null, int? retryCount = null)
    {
        if (!executors.ContainsKey(executorKey)) throw new OrchestratumException(
                $"Cannot append command: executor with type '{executorKey}' is not registered."
            );

        var dataSerialized = JsonSerializer.Serialize(data, dataType);
        var commandDbo = new CommandDbo()
        {
            Executor = executorKey,
            Target = targetKey ?? InstanceKey,
            DataType = dataType.AssemblyQualifiedName!,
            Data = dataSerialized,
            RetriesLeft = retryCount ?? defaultRetryCount,
            Timeout = timeout ?? defaultTimeout
        };

        using (var context = new OrchestratumDbContext(contextOptions))
        {
            context.Add(commandDbo);
            await context.SaveChangesAsync();
        }
        var command = new CommandHelper(this, commandDbo.Id);
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
        using var context = new OrchestratumDbContext(contextOptions);
        var commandDbos = context.Set<CommandDbo>();
        var actualCommands = await commandDbos.Where(c => !c.IsCompleted && !c.IsFailed && c.Target == InstanceKey).Select(c => c.Id).ToListAsync(cancellationToken);
        foreach (var commandId in actualCommands)
        {
            if (commands.Any(c => c.CommandId == commandId)) continue;
            commands.Add(new CommandHelper(this, commandId));
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
