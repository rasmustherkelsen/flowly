using System.Reflection;
using System.Text.RegularExpressions;

namespace Flowly.MessageInfrastructure.Events;

internal static class EventNameResolver
{
    public static string Resolve<TEvent>()
    {
        return Resolve(typeof(TEvent));
    }

    public static string Resolve(Type eventType)
    {
        var attribute = eventType.GetCustomAttribute<EventNameAttribute>();
        return attribute?.Name ?? DeriveEventName(eventType);
    }

    public static string DeriveSubscriptionName(Type handlerType)
    {
        var name = handlerType.Name;
        var kebab = Regex.Replace(name, @"(?<=[a-z])(?=[A-Z])", "-");
        return kebab.ToLowerInvariant();
    }

    private static string DeriveEventName(Type eventType)
    {
        var name = eventType.Name;

        if (name.EndsWith("Event", StringComparison.Ordinal))
            name = name[..^"Event".Length];

        var kebab = Regex.Replace(name, @"(?<=[a-z])(?=[A-Z])", "-");

        return kebab.ToLowerInvariant();
    }
}