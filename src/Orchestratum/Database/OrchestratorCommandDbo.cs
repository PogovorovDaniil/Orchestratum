using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Orchestratum.Database;

/// <summary>
/// Database entity representing an orchestrator command.
/// </summary>
[Table("orchestrator_commands")]
public class OrchestratorCommandDbo
{
    /// <summary>
    /// Unique identifier for the command.
    /// </summary>
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// The key of the executor that will process this command.
    /// </summary>
    [Column("executor")]
    public required string Executor { get; set; }

    /// <summary>
    /// The assembly-qualified name of the data type for deserialization.
    /// </summary>
    [Column("data_type")]
    public required string DataType { get; set; }

    /// <summary>
    /// JSON-serialized command data.
    /// </summary>
    [Column("data")]
    public required string Data { get; set; }

    /// <summary>
    /// Maximum time allowed for command execution.
    /// </summary>
    [Column("timeout")]
    public TimeSpan Timeout { get; set; }

    /// <summary>
    /// Number of retry attempts remaining for this command.
    /// </summary>
    [Column("retries_left")]
    public int RetriesLeft { get; set; }

    /// <summary>
    /// Indicates whether the command is currently being executed.
    /// </summary>
    [Column("is_running")]
    public bool IsRunning { get; set; }

    /// <summary>
    /// Time when the execution lock expires, allowing another instance to pick up the command.
    /// </summary>
    [Column("run_expires_at")]
    public DateTimeOffset? RunExpiresAt { get; set; }

    /// <summary>
    /// Indicates whether the command has been successfully completed.
    /// </summary>
    [Column("is_completed")]
    public bool IsCompleted { get; set; }

    /// <summary>
    /// Time when the command was completed.
    /// </summary>
    [Column("complete_at")]
    public DateTimeOffset? CompleteAt { get; set; }

    /// <summary>
    /// Indicates whether the command has failed after exhausting all retry attempts.
    /// </summary>
    [Column("is_failed")]
    public bool IsFailed { get; set; }
}
