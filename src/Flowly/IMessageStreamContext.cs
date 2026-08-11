namespace Flowly;

/// <summary>
///     The context passed to <see cref="MessageStreamHandler{TMessage}.Handle" /> for each batch of stream messages.
///     Contains the accumulated messages and a cancellation token signaling when processing should stop.
/// </summary>
/// <typeparam name="T">The message contract type.</typeparam>
public interface IMessageStreamContext<T>
{
    /// <summary>
    ///     The messages in this batch, in stream order. Batch size is bounded by
    ///     <see cref="MessageStreamHandlerOptions.MaxMessagesBeforeProcessing" /> and
    ///     <see cref="MessageStreamHandlerOptions.MaxWaitTime" />.
    /// </summary>
    IReadOnlyCollection<T> Messages { get; }

    /// <summary>
    ///     A cancellation token that is signaled when the host is shutting down and processing should stop.
    /// </summary>
    CancellationToken CancellationToken { get; }
}
