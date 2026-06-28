using System.Reflection;
using System.Text.RegularExpressions;

namespace Flowly.MessageInfrastructure;

/// <summary>
///     Base class for <see cref="ITopologyNameResolver" /> implementations that derive broker names from .NET type names
///     by converting PascalCase to a lowercase separated form. The word separator (e.g. <c>-</c> for kebab-case or
///     <c>.</c> for dot-case) is supplied by the subclass constructor. Attribute overrides via
///     <see cref="QueueNameAttribute" /> and <see cref="EventNameAttribute" /> are always respected.
/// </summary>
public abstract class SeparatedCaseTopologyNameResolver : ITopologyNameResolver
{
    private readonly string _separator;

    /// <summary>
    ///     Initialises the resolver with the given word separator.
    /// </summary>
    /// <param name="separator">
    ///     The string inserted between words when converting PascalCase type names to lowercase separated form
    ///     (e.g. <c>"-"</c> produces kebab-case, <c>"."</c> produces dot-case).
    /// </param>
    protected SeparatedCaseTopologyNameResolver(string separator)
    {
        _separator = separator;
    }

    /// <summary>
    ///     Resolves the queue name for <typeparamref name="TMessage" />. Returns the value of
    ///     <see cref="QueueNameAttribute" /> if present on the type; otherwise derives a separated-case name from the type
    ///     name with any trailing <c>Message</c> suffix stripped (e.g. <c>ProcessOrderMessage</c> with separator <c>-</c>
    ///     → <c>process-order</c>; with separator <c>.</c> → <c>process.order</c>).
    /// </summary>
    /// <typeparam name="TMessage">The message contract type to resolve a queue name for.</typeparam>
    /// <returns>The resolved queue name in lowercase separated form.</returns>
    public string ResolveQueueName<TMessage>()
    {
        var attribute = typeof(TMessage).GetCustomAttribute<QueueNameAttribute>();
        return attribute?.QueueName ?? DeriveName(typeof(TMessage).Name, "Message");
    }

    /// <summary>
    ///     Resolves the topic or exchange name for <typeparamref name="TEvent" />. Returns the value of
    ///     <see cref="EventNameAttribute" /> if present on the type; otherwise derives a separated-case name from the type
    ///     name with any trailing <c>Event</c> suffix stripped (e.g. <c>OrderPlacedEvent</c> with separator <c>-</c>
    ///     → <c>order-placed</c>; with separator <c>.</c> → <c>order.placed</c>).
    /// </summary>
    /// <typeparam name="TEvent">The event contract type to resolve a topic or exchange name for.</typeparam>
    /// <returns>The resolved topic or exchange name in lowercase separated form.</returns>
    public string ResolveEventName<TEvent>()
    {
        var attribute = typeof(TEvent).GetCustomAttribute<EventNameAttribute>();
        return attribute?.Name ?? DeriveName(typeof(TEvent).Name, "Event");
    }

    /// <summary>
    ///     Resolves the subscription name for <typeparamref name="THandler" />. Derives a separated-case name directly from
    ///     the handler type name without stripping any suffix (e.g. <c>EmailNotificationHandler</c> with separator <c>-</c>
    ///     → <c>email-notification-handler</c>; with separator <c>.</c> → <c>email.notification.handler</c>).
    /// </summary>
    /// <typeparam name="THandler">The event handler type to resolve a subscription name for.</typeparam>
    /// <returns>The resolved subscription name in lowercase separated form.</returns>
    public string ResolveSubscriptionName<THandler>()
        => ToSeparatedCase(typeof(THandler).Name);

    /// <inheritdoc />
    public string ResolveReplyQueueName(string callQueueName, string instanceName)
    {
        var allowedChars = $"[^a-z0-9{Regex.Escape(_separator)}]";
        var normalizedInstanceName = Regex.Replace(instanceName.ToLowerInvariant(), allowedChars, _separator);

        return $"{callQueueName}{_separator}reply{_separator}{normalizedInstanceName}";
    }

    private string DeriveName(string typeName, string suffix)
    {
        var name = typeName.EndsWith(suffix, StringComparison.Ordinal)
            ? typeName[..^suffix.Length]
            : typeName;

        return ToSeparatedCase(name);
    }

    private string ToSeparatedCase(string name)
        => Regex.Replace(name, @"(?<=[a-z])(?=[A-Z])", _separator).ToLowerInvariant();
}
