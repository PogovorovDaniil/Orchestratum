using Orchestratum.Contract;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Orchestratum.Services;

internal class CommandNameHelper
{
    private static readonly ConcurrentDictionary<Type, string> cachedCommandNames = [];

    public static string GetCommandName(Type type)
    {
        if (cachedCommandNames.ContainsKey(type)) return cachedCommandNames[type];

        var attribute = type.GetCustomAttribute<OrchCommandAttribute>();
        if (attribute is not null) return cachedCommandNames[type] = attribute.Name;

        var name = Regex.Replace(type.Name, @"Command$", "");
        name = Regex.Replace(name, @"(?<!^)(?=[A-Z])", "_");
        return cachedCommandNames[type] = name.ToLowerInvariant();
    }

    public static string GetCommandNameByHandler(Type handlerType)
    {
        if (handlerType is null)
            throw new ArgumentNullException(nameof(handlerType));

        var interfaceType = handlerType
            .GetInterfaces()
            .SingleOrDefault(i => i.IsGenericType && typeof(IOrchCommandHandler<>) == i.GetGenericTypeDefinition());

        if (interfaceType is null)
            throw new ArgumentException($"Type '{handlerType.FullName}' does not implement '{nameof(IOrchCommandHandler<>)}'.", nameof(handlerType));

        return GetCommandName(interfaceType.GetGenericArguments()[0]);
    }
}
