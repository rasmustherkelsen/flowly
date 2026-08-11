namespace Flowly.InMemory;

/// <summary>
///     Configuration options for the in-memory Flowly transport provider.
/// </summary>
public class InMemoryOptions
{
    /// <summary>
    ///     The maximum allowed message body size in bytes. Messages exceeding this limit throw a
    ///     <see cref="Flowly.Transport.MessageTooLargeException" />. Defaults to 1 MB (1 048 576 bytes).
    /// </summary>
    public long MaxMessageSizeBytes { get; set; } = 1_048_576;

    /// <summary>
    ///     The bounded capacity of each in-memory channel. When a channel is full, writers will block until space is
    ///     available, applying back-pressure analogous to a real broker. Defaults to 1000.
    /// </summary>
    public int ChannelCapacity { get; set; } = 1000;

    /// <summary>
    ///     When <see langword="true" />, messages are passed as object references through in-memory channels instead of
    ///     being serialised to JSON on send and deserialised on receive. Preserves object identity, supports
    ///     non-serialisable types, and removes the JSON round-trip overhead. <see cref="MaxMessageSizeBytes" /> is not
    ///     enforced when this option is enabled. Defaults to <see langword="false" />.
    ///     <para>
    ///         For message stream queues (<see cref="MessageStreamHandler{TMessage}" /> / <see cref="IMessageRecorder" />),
    ///         enabling this also disables <see cref="StreamRetentionAttribute.MaxLengthBytes" /> retention —
    ///         reference-passed messages are never serialised, so there is no byte size to account against. Only
    ///         <see cref="StreamRetentionAttribute.MaxAgeSeconds" /> retention still applies to streams in this mode.
    ///     </para>
    /// </summary>
    public bool EnableReferencePassing { get; set; } = false;
}
