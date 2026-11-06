namespace Flowly;

/// <summary>
///     Interface representing the context for processing a batch of messages of type T. This includes the collection of
///     messages and a cancellation token to signal when processing should be cancelled.
/// </summary>
/// <typeparam name="T"></typeparam>
public interface IBatchMessageContext<T>
{
    /// <summary>
    ///     The collection of messages to be processed in this batch.
    /// </summary>
    IReadOnlyCollection<T> Messages { get; }

    /// <summary>
    ///     A cancellation token that can be used to signal when processing of the batch should be cancelled or if the
    ///     processing should be aborted.
    /// </summary>
    CancellationToken CancellationToken { get; }

    /// <summary>
    ///     The transport-level properties of each message in <see cref="Messages" />, one entry per message in the same
    ///     order, including the application-defined <see cref="MessageProperties.CustomProperties" /> the sender attached.
    ///     The custom properties are not authenticated and must not be treated as proof of identity.
    /// </summary>
    IReadOnlyList<MessageProperties> Properties { get; }
}