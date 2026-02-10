using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Orchestratum.Database;

/// <summary>
/// Database entity representing a command in the orchestration system.
/// </summary>
public class OrchCommandDbo
{
    /// <summary>
    /// Gets or sets the unique identifier of the command.
    /// </summary>
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the target instance key that should execute this command.
    /// </summary>
    [Column("target")]
    public required string Target { get; set; }

    /// <summary>
    /// Gets or sets the name of the command.
    /// </summary>
    [Column("name")]
    public required string Name { get; set; }

    /// <summary>
    /// Gets or sets the JSON-serialized input data for the command.
    /// </summary>
    [Column("input")]
    public string? Input { get; set; }

    /// <summary>
    /// Gets or sets the JSON-serialized output data produced by the command.
    /// </summary>
    [Column("output")]
    public string? Output { get; set; }

    /// <summary>
    /// Gets or sets when the command is scheduled to be executed.
    /// </summary>
    [Column("scheduled_at")]
    public DateTimeOffset ScheduledAt { get; set; }

    /// <summary>
    /// Gets or sets the maximum time allowed for command execution.
    /// </summary>
    [Column("timeout")]
    public TimeSpan Timeout { get; set; }


    /// <summary>
    /// Gets or sets a value indicating whether the command is currently being executed.
    /// </summary>
    [Column("is_running")]
    public bool IsRunning { get; set; }

    /// <summary>
    /// Gets or sets when the command execution started.
    /// </summary>
    [Column("running_at")]
    public DateTimeOffset? RunningAt { get; set; }

    /// <summary>
    /// Gets or sets when the execution lock expires.
    /// </summary>
    [Column("run_expires_at")]
    public DateTimeOffset? RunExpiresAt { get; set; }


    /// <summary>
    /// Gets or sets a value indicating whether the command completed successfully.
    /// </summary>
    [Column("is_completed")]
    public bool IsCompleted { get; set; }

    /// <summary>
    /// Gets or sets when the command completed successfully.
    /// </summary>
    [Column("completed_at")]
    public DateTimeOffset? CompletedAt { get; set; }


    /// <summary>
    /// Gets or sets a value indicating whether the command was cancelled.
    /// </summary>
    [Column("is_canceled")]
    public bool IsCanceled { get; set; }

    /// <summary>
    /// Gets or sets when the command was cancelled.
    /// </summary>
    [Column("canceled_at")]
    public DateTimeOffset? CanceledAt { get; set; }


    /// <summary>
    /// Gets or sets the number of retry attempts remaining for this command.
    /// </summary>
    [Column("retries_left")]
    public int RetriesLeft { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the command failed permanently.
    /// </summary>
    [Column("is_failed")]
    public bool IsFailed { get; set; }

    /// <summary>
    /// Gets or sets when the command failed permanently.
    /// </summary>
    [Column("failed_at")]
    public DateTimeOffset? FailedAt { get; set; }
}
