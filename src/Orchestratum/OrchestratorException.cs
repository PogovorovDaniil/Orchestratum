namespace Orchestratum;

/// <summary>
/// Exception thrown when an error occurs in the orchestrator.
/// </summary>
public class OrchestratorException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OrchestratorException"/> class with a message and inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception that caused this exception.</param>
    public OrchestratorException(string? message, Exception? innerException) : base(message, innerException) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="OrchestratorException"/> class with a message.
    /// </summary>
    /// <param name="message">The error message.</param>
    public OrchestratorException(string message) : base(message) { }
}