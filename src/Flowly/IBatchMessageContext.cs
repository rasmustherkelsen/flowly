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
}