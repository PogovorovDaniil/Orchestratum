using Microsoft.EntityFrameworkCore;
using Orchestratum.Contract;
using Orchestratum.Database;
using Orchestratum.Services;
using System.Data;
using System.Reflection;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Configuration options for the Orchestratum service.
/// </summary>
public class OrchServiceConfiguration
{
    private static Type[] GetHandlerTypes(IEnumerable<Assembly> assemblies) => assemblies
        .SelectMany(a => a.GetTypes())
        .Where(t => t.IsClass && !t.IsAbstract)
        .Where(t => typeof(IOrchCommandHandler).IsAssignableFrom(t))
        .ToArray();

    private static Type[] GetCommandTypes(IEnumerable<Assembly> assemblies) => assemblies
        .SelectMany(a => a.GetTypes())
        .Where(t => t.IsClass && !t.IsAbstract)
        .Where(t => typeof(IOrchCommand).IsAssignableFrom(t))
        .ToArray();

    private static void EmptyLog(string text) { }

    internal DbContextOptionsBuilder<OrchDbContext> ContextOptionsBuilder = new DbContextOptionsBuilder<OrchDbContext>();
    internal List<ServiceDescriptor> CommandDescriptors { get; } = [];
    internal List<ServiceDescriptor> HandlerDescriptors { get; } = [];

    /// <summary>
    /// Configures the database context for storing orchestration data.
    /// </summary>
    /// <param name="optionsAction">Action to configure the database options.</param>
    /// <param name="debugDatabase">If true, enables sensitive data logging for debugging.</param>
    /// <returns>The configuration instance for method chaining.</returns>
    public OrchServiceConfiguration ConfigureDbContext(Action<DbContextOptionsBuilder> optionsAction, bool debugDatabase = false)
    {
        if (!debugDatabase) ContextOptionsBuilder.EnableSensitiveDataLogging(false).LogTo(EmptyLog);
        optionsAction(ContextOptionsBuilder);
        return this;
    }

    /// <summary>
    /// Registers all commands from the specified assemblies.
    /// </summary>
    /// <param name="assemblies">The assemblies to scan for command types.</param>
    /// <returns>The configuration instance for method chaining.</returns>
    public OrchServiceConfiguration RegisterCommands(params Assembly[] assemblies)
    {
        var commands = GetCommandTypes(assemblies);
        foreach (var command in commands) RegisterCommand(command);
        return this;
    }

    /// <summary>
    /// Registers a specific command type.
    /// </summary>
    /// <param name="type">The command type to register.</param>
    /// <param name="serviceLifetime">The service lifetime for the command.</param>
    /// <returns>The configuration instance for method chaining.</returns>
    public OrchServiceConfiguration RegisterCommand(Type type, ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
    {
        HandlerDescriptors.Add(new ServiceDescriptor(typeof(IOrchCommand), CommandNameHelper.GetCommandName(type), type, serviceLifetime));
        return this;
    }

    /// <summary>
    /// Registers all command handlers from the specified assemblies.
    /// </summary>
    /// <param name="assemblies">The assemblies to scan for handler types.</param>
    /// <returns>The configuration instance for method chaining.</returns>
    public OrchServiceConfiguration RegisterHandlers(params Assembly[] assemblies)
    {
        var handlers = GetHandlerTypes(assemblies);
        foreach (var handler in handlers) RegisterHandler(handler);
        return this;
    }

    /// <summary>
    /// Registers a specific command handler type.
    /// </summary>
    /// <param name="type">The handler type to register.</param>
    /// <param name="serviceLifetime">The service lifetime for the handler.</param>
    /// <returns>The configuration instance for method chaining.</returns>
    public OrchServiceConfiguration RegisterHandler(Type type, ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
    {
        HandlerDescriptors.Add(new ServiceDescriptor(typeof(IOrchCommandHandler), CommandNameHelper.GetCommandNameByHandler(type), type, serviceLifetime));
        return this;
    }

    /// <summary>
    /// Registers a specific command handler type using generics.
    /// </summary>
    /// <typeparam name="THandler">The handler type to register.</typeparam>
    /// <param name="serviceLifetime">The service lifetime for the handler.</param>
    /// <returns>The configuration instance for method chaining.</returns>
    public OrchServiceConfiguration RegisterHandler<THandler>(ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
        where THandler : IOrchCommandHandler => RegisterHandler(typeof(THandler), serviceLifetime);

    /// <summary>
    /// Gets or sets the interval between polling cycles for new commands.
    /// Default is 5 seconds.
    /// </summary>
    public TimeSpan CommandPollingInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets or sets the buffer time added to command timeout for lock expiration.
    /// Default is 10 seconds.
    /// </summary>
    public TimeSpan LockTimeoutBuffer { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Gets or sets the maximum number of commands to pull in one polling cycle.
    /// Default is 100.
    /// </summary>
    public int MaxCommandPull { get; set; } = 100;

    /// <summary>
    /// Gets or sets the instance key used for targeting commands to specific workers.
    /// Default is "default".
    /// </summary>
    public string InstanceKey { get; set; } = IOrchCommand.DefaultInstanceKey;

    /// <summary>
    /// Gets or sets the prefix for database table names.
    /// Default is "ORCH_".
    /// </summary>
    public string TablePrefix { get; set; } = "ORCH_";
}
