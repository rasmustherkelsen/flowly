using System.Reflection;
using System.Text.RegularExpressions;
using Flowly.MessageInfrastructure.Receivers;

namespace Flowly.MessageInfrastructure;

/// <summary>
///     Provides functionality to resolve the name of the message queue for a given message type. The resolver first checks
///     if the message type is decorated with a <see cref="QueueNameAttribute" /> and uses the specified queue name if
///     present. If the attribute is not found, it derives the queue name from the message type's name by removing any
///     "Message" suffix and converting it to kebab-case. This allows for flexible naming conventions while also providing
///     a default naming strategy based on the message type's name. The resolved queue name is used by message receivers to
///     determine which queue to listen to for incoming messages of that type.
/// </summary>
public static class MessageQueueNameResolver
{
    /// <summary>
    ///     Resolves the name of the message queue for the specified message type. It first checks if the message type is
    ///     decorated with a <see cref="QueueNameAttribute" /> and uses the specified queue name if present. If the attribute
    ///     is not found, it derives the queue name from the message type's name by removing any "Message" suffix and
    ///     converting it to kebab-case. This allows for flexible naming conventions while also providing a default naming
    ///     strategy based on the message type's name. The resolved queue name is used by message receivers to determine which
    ///     queue to listen to for incoming messages of that type.
    /// </summary>
    /// <typeparam name="TMessage">The message contract type to derive a queue name for.</typeparam>
    /// <returns>The resolved queue name in lowercase kebab-case.</returns>
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