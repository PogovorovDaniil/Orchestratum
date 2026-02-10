namespace Orchestratum.Contract;

/// <summary>
/// Specifies an explicit name for a command class.
/// </summary>
/// <param name="name">The name to use for the command.</param>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class OrchCommandAttribute(string name) : Attribute
{
    /// <summary>
    /// Gets the name of the command.
    /// </summary>
    public string Name { get; } = name;
}
