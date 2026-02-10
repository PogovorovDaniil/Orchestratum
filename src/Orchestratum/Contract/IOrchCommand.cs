namespace Orchestratum.Contract;

/// <summary>
/// Represents a command that can be orchestrated and executed asynchronously.
/// </summary>
public interface IOrchCommand
{
    /// <summary>
    /// The default instance key used for command targeting.
    /// </summary>
    public const string DefaultInstanceKey = "default";

    internal string Name { get; set; }

    /// <summary>
    /// Gets or sets the unique identifier of the command.
    /// </summary>
    Guid Id { get; internal set; }

    /// <summary>
    /// Gets or sets the target instance key that should execute this command.
    /// </summary>
    string? Target { get; set; }

    /// <summary>
    /// Gets or sets the maximum time allowed for command execution.
    /// </summary>
    TimeSpan Timeout { get; set; }

    /// <summary>
    /// Gets or sets the delay before the command becomes available for execution.
    /// </summary>
    TimeSpan Delay { get; set; }

    /// <summary>
    /// Gets or sets the number of retry attempts if the command fails.
    /// </summary>
    int RetryCount { get; set; }

    internal Type InputType { get; }
    internal object? Input { get; set; }
    internal IEnumerable<IOrchCommand> OnSuccess(object? output);
    internal IEnumerable<IOrchCommand> OnCancellation();
    internal IEnumerable<IOrchCommand> OnFailure();
}

/// <summary>
/// Base class for commands with input data only.
/// </summary>
/// <typeparam name="TInput">The type of input data for the command.</typeparam>
public abstract class OrchCommand<TInput> : IOrchCommand
    where TInput : notnull
{
    string IOrchCommand.Name { get; set; } = "";

    /// <summary>
    /// Gets or sets the unique identifier of the command.
    /// </summary>
    public Guid Id { get; internal set; } = Guid.NewGuid();
    Guid IOrchCommand.Id { get => Id; set => Id = value; }

    /// <summary>
    /// Gets or sets the target instance key that should execute this command. Default is "default".
    /// </summary>
    public virtual string? Target { get; set; }

    /// <summary>
    /// Gets or sets the maximum time allowed for command execution. Default is 5 seconds.
    /// </summary>
    public virtual TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets or sets the delay before the command becomes available for execution. Default is zero (execute immediately).
    /// </summary>
    public virtual TimeSpan Delay { get; set; } = TimeSpan.Zero;

    /// <summary>
    /// Gets or sets the number of retry attempts if the command fails. Default is 3.
    /// </summary>
    public virtual int RetryCount { get; set; } = 3;

    Type IOrchCommand.InputType => typeof(TInput);
    object? IOrchCommand.Input { get => Input; set => Input = (TInput)value!; }

    /// <summary>
    /// Gets or sets the input data for the command.
    /// </summary>
    public required TInput Input { get; set; }

    IEnumerable<IOrchCommand> IOrchCommand.OnSuccess(object? output) => OnSuccess();

    /// <summary>
    /// Returns commands to be executed after successful completion of this command.
    /// </summary>
    /// <returns>A collection of commands to execute next.</returns>
    protected virtual IEnumerable<IOrchCommand> OnSuccess() => [];

    IEnumerable<IOrchCommand> IOrchCommand.OnCancellation() => OnCancellation();

    /// <summary>
    /// Returns commands to be executed if this command is cancelled. Default behavior is to execute OnFailure commands.
    /// </summary>
    /// <returns>A collection of commands to execute on cancellation.</returns>
    protected virtual IEnumerable<IOrchCommand> OnCancellation() => OnFailure();

    IEnumerable<IOrchCommand> IOrchCommand.OnFailure() => OnFailure();

    /// <summary>
    /// Returns commands to be executed if this command fails permanently.
    /// </summary>
    /// <returns>A collection of commands to execute on failure.</returns>
    protected virtual IEnumerable<IOrchCommand> OnFailure() => [];

    /// <summary>
    /// Creates a result object for this command.
    /// </summary>
    /// <param name="status">The execution status of the command. Default is Success.</param>
    /// <returns>A result object representing the command execution outcome.</returns>
    public IOrchResult<OrchCommand<TInput>> CreateResult(OrchResultStatus status = OrchResultStatus.Success) => new OrchResult(status);

    internal readonly struct OrchResult(OrchResultStatus status) : IOrchResult<OrchCommand<TInput>>
    {
        public OrchResultStatus Status { get; } = status;
        object? IOrchResult.Output => null;
    }
}

/// <summary>
/// Base class for commands with input and output data.
/// </summary>
/// <typeparam name="TInput">The type of input data for the command.</typeparam>
/// <typeparam name="TOutput">The type of output data produced by the command.</typeparam>
public abstract class OrchCommand<TInput, TOutput> : IOrchCommand
    where TInput : notnull
    where TOutput : notnull
{
    string IOrchCommand.Name { get; set; } = "";

    /// <summary>
    /// Gets or sets the unique identifier of the command.
    /// </summary>
    public Guid Id { get; internal set; } = Guid.NewGuid();
    Guid IOrchCommand.Id { get => Id; set => Id = value; }

    /// <summary>
    /// Gets or sets the target instance key that should execute this command. Default is "default".
    /// </summary>
    public virtual string? Target { get; set; }

    /// <summary>
    /// Gets or sets the maximum time allowed for command execution. Default is 5 seconds.
    /// </summary>
    public virtual TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets or sets the delay before the command becomes available for execution. Default is zero (execute immediately).
    /// </summary>
    public virtual TimeSpan Delay { get; set; } = TimeSpan.Zero;

    /// <summary>
    /// Gets or sets the number of retry attempts if the command fails. Default is 3.
    /// </summary>
    public virtual int RetryCount { get; set; } = 3;

    Type IOrchCommand.InputType => typeof(TInput);
    object? IOrchCommand.Input { get => Input; set => Input = (TInput)value!; }

    /// <summary>
    /// Gets or sets the input data for the command.
    /// </summary>
    public required TInput Input { get; set; }

    IEnumerable<IOrchCommand> IOrchCommand.OnSuccess(object? output) => OnSuccess((TOutput)output!);

    /// <summary>
    /// Returns commands to be executed after successful completion of this command.
    /// </summary>
    /// <param name="output">The output data produced by the command execution.</param>
    /// <returns>A collection of commands to execute next.</returns>
    protected virtual IEnumerable<IOrchCommand> OnSuccess(TOutput output) => [];

    IEnumerable<IOrchCommand> IOrchCommand.OnCancellation() => OnCancellation();

    /// <summary>
    /// Returns commands to be executed if this command is cancelled. Default behavior is to execute OnFailure commands.
    /// </summary>
    /// <returns>A collection of commands to execute on cancellation.</returns>
    protected virtual IEnumerable<IOrchCommand> OnCancellation() => OnFailure();

    IEnumerable<IOrchCommand> IOrchCommand.OnFailure() => OnFailure();

    /// <summary>
    /// Returns commands to be executed if this command fails permanently.
    /// </summary>
    /// <returns>A collection of commands to execute on failure.</returns>
    protected virtual IEnumerable<IOrchCommand> OnFailure() => [];

    /// <summary>
    /// Creates a result object for this command with output data.
    /// </summary>
    /// <param name="output">The output data produced by the command execution.</param>
    /// <param name="status">The execution status of the command. Default is Success.</param>
    /// <returns>A result object representing the command execution outcome.</returns>
    public IOrchResult<OrchCommand<TInput, TOutput>> CreateResult(TOutput output, OrchResultStatus status = OrchResultStatus.Success) => new OrchResult(output, status);

    internal readonly struct OrchResult(TOutput output, OrchResultStatus status) : IOrchResult<OrchCommand<TInput, TOutput>>
    {
        public OrchResultStatus Status { get; } = status;
        public TOutput Output { get; } = output;
        object? IOrchResult.Output => Output;
    }
}
