namespace Orchestratum.Contract;

/// <summary>
/// Represents the result of a command execution.
/// </summary>
public interface IOrchResult
{
    /// <summary>
    /// Gets the status of the command execution.
    /// </summary>
    public OrchResultStatus Status { get; }

    /// <summary>
    /// Gets the output data produced by the command execution, if any.
    /// </summary>
    public object? Output { get; }
}

/// <summary>
/// Represents a typed result of a command execution.
/// </summary>
/// <typeparam name="TCommand">The type of command that produced this result.</typeparam>
public interface IOrchResult<in TCommand> : IOrchResult;

internal readonly struct EmptyOrchResult(OrchResultStatus status) : IOrchResult
{
    public OrchResultStatus Status { get; } = status;
    public object? Output => null;
}

/// <summary>
/// Specifies the execution status of a command.
/// </summary>
public enum OrchResultStatus
{
    /// <summary>
    /// The command executed successfully.
    /// </summary>
    Success,

    /// <summary>
    /// The command was cancelled during execution.
    /// </summary>
    Cancelled,

    /// <summary>
    /// The command failed during execution.
    /// </summary>
    Failed,

    /// <summary>
    /// The command handler was not found.
    /// </summary>
    NotFound,

    /// <summary>
    /// The command execution exceeded the allowed timeout.
    /// </summary>
    TimedOut,
}
