namespace Flowly;

/// <summary>
///     Interface representing the context for processing a single message of type T. This includes the message itself and
///     a cancellation token to signal when processing should be cancelled. This context is used by message handlers to
///     access the message data and to handle cancellation requests appropriately during message processing.
/// </summary>
/// <typeparam name="T"></typeparam>
public interface IMessageContext<T>
{
    /// <summary>
    ///     The message to be processed. This property provides access to the actual message object that is being processed.
    ///     The type of the message is specified by the generic type parameter T, allowing for strong typing and flexibility in
    ///     handling
    /// </summary>
    T Message { get; }

    /// <summary>
    ///     A cancellation token that can be used to signal when processing of the message should be cancelled or if the
    ///     processing should be aborted.
    /// </summary>
    CancellationToken CancellationToken { get; }
}