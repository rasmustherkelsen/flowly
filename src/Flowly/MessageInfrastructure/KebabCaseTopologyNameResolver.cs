using System.Reflection;
using System.Text.RegularExpressions;

namespace Flowly.MessageInfrastructure;

/// <summary>
///     The default <see cref="ITopologyNameResolver" /> implementation. Derives broker names from .NET type names using
///     kebab-case conversion, with optional overrides via <see cref="QueueNameAttribute" /> and
///     <see cref="EventNameAttribute" />. PascalCase type names are split on uppercase boundaries and joined with
///     <c>-</c>; a trailing <c>Message</c> or <c>Event</c> suffix is stripped before conversion.
/// </summary>
public class KebabCaseTopologyNameResolver : ITopologyNameResolver
{
    /// <summary>
    ///     Resolves the queue name for <typeparamref name="TMessage" />. Returns the value of
    ///     <see cref="QueueNameAttribute" /> if present on the type; otherwise derives a kebab-case name from the type name
    ///     with any trailing <c>Message</c> suffix stripped (e.g. <c>ProcessOrderMessage</c> → <c>process-order</c>).
    /// </summary>
    /// <typeparam name="TMessage">The message contract type to resolve a queue name for.</typeparam>
    /// <returns>The resolved queue name in lowercase kebab-case.</returns>
    public string ResolveQueueName<TMessage>()
    {
        var attribute = typeof(TMessage).GetCustomAttribute<QueueNameAttribute>();
        return attribute?.QueueName ?? DeriveKebabName(typeof(TMessage).Name, "Message");
    }

    /// <summary>
    ///     Resolves the topic or exchange name for <typeparamref name="TEvent" />. Returns the value of
    ///     <see cref="EventNameAttribute" /> if present on the type; otherwise derives a kebab-case name from the type name
    ///     with any trailing <c>Event</c> suffix stripped (e.g. <c>OrderPlacedEvent</c> → <c>order-placed</c>).
    /// </summary>
    /// <typeparam name="TEvent">The event contract type to resolve a topic or exchange name for.</typeparam>
    /// <returns>The resolved topic or exchange name in lowercase kebab-case.</returns>
    public string ResolveEventName<TEvent>()
    {
        var attribute = typeof(TEvent).GetCustomAttribute<EventNameAttribute>();
        return attribute?.Name ?? DeriveKebabName(typeof(TEvent).Name, "Event");
    }

    /// <summary>
    ///     Resolves the subscription name for <typeparamref name="THandler" />. Derives a kebab-case name directly from the
    ///     handler type name without stripping any suffix (e.g. <c>EmailNotificationHandler</c> →
    ///     <c>email-notification-handler</c>).
    /// </summary>
    /// <typeparam name="THandler">The event handler type to resolve a subscription name for.</typeparam>
    /// <returns>The resolved subscription name in lowercase kebab-case.</returns>
    public string ResolveSubscriptionName<THandler>()
        => ToKebabCase(typeof(THandler).Name);

    /// <inheritdoc />
    public string ResolveReplyQueueName(string callQueueName, string instanceName)
        => $"{callQueueName}-reply-{Regex.Replace(instanceName.ToLowerInvariant(), @"[^a-z0-9-]", "-")}";

    private static string DeriveKebabName(string typeName, string suffix)
    {
        var name = typeName.EndsWith(suffix, StringComparison.Ordinal)
            ? typeName[..^suffix.Length]
            : typeName;

        return ToKebabCase(name);
    }

    private static string ToKebabCase(string name)
        => Regex.Replace(name, @"(?<=[a-z])(?=[A-Z])", "-").ToLowerInvariant();
}
