namespace Orchestratum;

/// <summary>
/// Exception thrown when an error occurs in the orchestratum.
/// </summary>
public class OrchestratumException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OrchestratumException"/> class with a message and inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception that caused this exception.</param>
    public OrchestratumException(string? message, Exception? innerException) : base(message, innerException) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="OrchestratumException"/> class with a message.
    /// </summary>
    /// <param name="message">The error message.</param>
    public OrchestratumException(string message) : base(message) { }
}