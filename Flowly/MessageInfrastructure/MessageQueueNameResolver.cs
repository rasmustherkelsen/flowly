using System.Reflection;
using System.Text.RegularExpressions;
using Flowly.MessageInfrastructure.Receivers;

namespace Flowly.MessageInfrastructure;

public static class MessageQueueNameResolver
{
    public static string Resolve<TMessage>()
    {
        var queueNameAttribute = typeof(TMessage).GetCustomAttribute<QueueNameAttribute>();
        return queueNameAttribute?.QueueName ?? DeriveQueueName(typeof(TMessage));
    }

    private static string DeriveQueueName(Type messageType)
    {
        var name = messageType.Name;
        if (name.EndsWith("Message", StringComparison.Ordinal))
            name = name[..^"Message".Length];
        var kebab = Regex.Replace(name, @"(?<=[a-z])(?=[A-Z])", "-");
        return kebab.ToLowerInvariant();
    }
}
