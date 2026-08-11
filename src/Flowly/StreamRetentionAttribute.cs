namespace Flowly;

/// <summary>
///     Attribute to specify retention limits for the stream queue backing a message contract. Apply to the
///     <strong>message contract</strong> class (not the handler), alongside attributes like
///     <see cref="QueueNameAttribute" />. Only meaningful for contracts consumed via
///     <see cref="MessageStreamHandler{TMessage}" /> or recorded via <see cref="IMessageRecorder" /> — RabbitMQ maps
///     the values onto the stream's <c>x-max-age</c> and <c>x-max-length-bytes</c> arguments at topology creation.
///     <para>
///         When neither limit is set the stream retains every message indefinitely and grows unbounded — a broker
///         disk exhaustion risk. Set at least one limit for production streams.
///     </para>
/// </summary>
/// <param name="maxAgeSeconds">
///     The maximum age of retained messages in seconds, or <see langword="null" /> for no age limit.
/// </param>
/// <param name="maxLengthBytes">
///     The maximum total size of the stream in bytes, or <see langword="null" /> for no size limit.
/// </param>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class StreamRetentionAttribute(int maxAgeSeconds = 0, long maxLengthBytes = 0) : Attribute
{
    /// <summary>
    ///     The maximum age of retained messages in seconds, or <see langword="null" /> when no age limit was specified.
    /// </summary>
    public int? MaxAgeSeconds { get; } = maxAgeSeconds > 0 ? maxAgeSeconds : null;

    /// <summary>
    ///     The maximum total size of the stream in bytes, or <see langword="null" /> when no size limit was specified.
    /// </summary>
    public long? MaxLengthBytes { get; } = maxLengthBytes > 0 ? maxLengthBytes : null;
}
