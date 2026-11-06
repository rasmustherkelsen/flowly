using System.Collections.Frozen;
using System.Text;

namespace Flowly.Transport;

/// <summary>
///     Rules and helpers for the application-defined key/value pairs that travel with a message as
///     <see cref="MessageProperties.CustomProperties" />. Senders validate the pairs with <see cref="Validate" /> before
///     anything leaves the process, and each transport maps them to its native concept (RabbitMQ headers, Azure Service
///     Bus application properties, Azure Event Hubs event properties, InMemory envelope properties) on send and reads them
///     back with <see cref="Read" /> on receive.
/// </summary>
/// <remarks>
///     Custom properties are plain strings, the one value type every transport round-trips unchanged. They are not
///     authenticated: anyone who can publish to the destination can set any value, so they must not be treated as proof of
///     identity unless the value is signed by the application.
/// </remarks>
public static class CustomMessageProperties
{
    /// <summary>
    ///     An empty, immutable set of custom properties. The value of <see cref="MessageProperties.CustomProperties" /> for a
    ///     message that carries none.
    /// </summary>
    public static IReadOnlyDictionary<string, string> Empty { get; } = FrozenDictionary<string, string>.Empty;

    /// <summary>
    ///     Returns whether <paramref name="key" /> is reserved for Flowly and the W3C trace context: it starts with
    ///     <see cref="FlowlyMessageProperties.ReservedPrefix" /> or is <c>traceparent</c> or <c>tracestate</c>. The comparison
    ///     ignores case. Reserved keys can neither be sent as custom properties nor appear in
    ///     <see cref="MessageProperties.CustomProperties" /> on a received message.
    /// </summary>
    /// <param name="key">The property key to test.</param>
    /// <returns><see langword="true" /> when the key is reserved; otherwise <see langword="false" />.</returns>
    public static bool IsReserved(string key)
        => key.StartsWith(FlowlyMessageProperties.ReservedPrefix, StringComparison.OrdinalIgnoreCase)
           || key.Equals(FlowlyMessageProperties.Traceparent, StringComparison.OrdinalIgnoreCase)
           || key.Equals(FlowlyMessageProperties.Tracestate, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    ///     Throws when <paramref name="customProperties" /> contains a key that is empty or reserved (see
    ///     <see cref="IsReserved" />) or a <see langword="null" /> value. A <see langword="null" /> dictionary is valid and
    ///     means no custom properties.
    /// </summary>
    /// <param name="customProperties">The custom properties supplied by the caller, or <see langword="null" />.</param>
    /// <param name="paramName">The name of the caller's parameter, reported by the thrown exception.</param>
    /// <exception cref="ArgumentException">A key is empty, whitespace, reserved, or its value is <see langword="null" />.</exception>
    public static void Validate(IReadOnlyDictionary<string, string>? customProperties, string paramName)
    {
        if (customProperties is null)
        {
            return;
        }

        foreach (var (key, value) in customProperties)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("A custom message property key cannot be empty.", paramName);
            }

            if (IsReserved(key))
            {
                throw new ArgumentException(
                    $"The custom message property key '{key}' is reserved. Keys starting with '{FlowlyMessageProperties.ReservedPrefix}' and the keys " +
                    $"'{FlowlyMessageProperties.Traceparent}' and '{FlowlyMessageProperties.Tracestate}' are used by Flowly.",
                    paramName);
            }

            if (value is null)
            {
                throw new ArgumentException($"The custom message property '{key}' has a null value.", paramName);
            }
        }
    }

    /// <summary>
    ///     Builds <see cref="MessageProperties.CustomProperties" /> from the native application properties of a received
    ///     message. Keeps the entries whose value is a <see cref="string" /> or a UTF-8 <see cref="byte" /> array and whose
    ///     key is not reserved (see <see cref="IsReserved" />); everything else, such as the integer retry count or
    ///     broker-set values of other types, is left out.
    /// </summary>
    /// <param name="nativeProperties">
    ///     The broker's headers, application properties or event properties, or <see langword="null" /> when the message
    ///     has none.
    /// </param>
    /// <returns>
    ///     The custom properties, or <see cref="Empty" /> when there are none.
    /// </returns>
    public static IReadOnlyDictionary<string, string> Read(IEnumerable<KeyValuePair<string, object?>>? nativeProperties)
    {
        if (nativeProperties is null)
        {
            return Empty;
        }

        Dictionary<string, string>? customProperties = null;

        foreach (var (key, value) in nativeProperties)
        {
            if (IsReserved(key) || ToText(value) is not { } text)
            {
                continue;
            }

            customProperties ??= new Dictionary<string, string>();
            customProperties[key] = text;
        }

        return customProperties ?? Empty;
    }

    private static string? ToText(object? value) => value switch
    {
        string text => text,
        byte[] bytes => Encoding.UTF8.GetString(bytes),
        _ => null
    };
}
