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
}
