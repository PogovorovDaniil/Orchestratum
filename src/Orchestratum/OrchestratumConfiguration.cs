using Microsoft.EntityFrameworkCore;
using Orchestratum.Database;

namespace Orchestratum;

/// <summary>
/// Configuration options for the orchestrator.
/// </summary>
public class OrchestratumConfiguration
{
    internal DbContextOptions<OrchestratumDbContext> contextOptions = new();
    internal Dictionary<string, ExecutorDelegate> storedExecutors = [];

    /// <summary>
    /// Registers a task executor with a unique key.
    /// </summary>
    /// <param name="executorKey">The unique key to identify this executor.</param>
    /// <param name="executorDelegate">The delegate function that will execute tasks.</param>
    /// <returns>The configuration instance for method chaining.</returns>
    public OrchestratumConfiguration RegisterExecutor(string executorKey, ExecutorDelegate executorDelegate)
    {
        storedExecutors[executorKey] = executorDelegate;
        return this;
    }

    /// <summary>
    /// Configures the database context for storing orchestrator commands.
    /// </summary>
    /// <param name="optionsAction">Action to configure the DbContext options.</param>
    /// <returns>The configuration instance for method chaining.</returns>
    public OrchestratumConfiguration ConfigureDbContext(Action<DbContextOptionsBuilder> optionsAction)
    {
        var optionsBuilder = new DbContextOptionsBuilder<OrchestratumDbContext>()
            .EnableSensitiveDataLogging(false)
            .LogTo(_ => { });
        optionsAction(optionsBuilder);
        contextOptions = optionsBuilder.Options;
        return this;
    }

    /// <summary>
    /// The interval at which the orchestratum polls the database for new commands.
    /// Default: 1 minute.
    /// </summary>
    public TimeSpan CommandPollingInterval { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Additional buffer time added to command timeout for lock expiration.
    /// Default: 1 second.
    /// </summary>
    public TimeSpan LockTimeoutBuffer { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Default timeout for command execution if not specified when appending.
    /// Default: 1 minute.
    /// </summary>
    public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Default number of retry attempts for failed commands if not specified when appending.
    /// Default: 3.
    /// </summary>
    public int DefaultRetryCount { get; set; } = 3;

    /// <summary>
    /// Key of this orchestrator instance.
    /// Used to distinguish between multiple running instances when coordinating command execution.
    /// Default: "default".
    /// </summary>
    public string InstanceKey { get; set; } = "default";
}
