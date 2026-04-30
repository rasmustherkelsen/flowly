namespace Flowly.Transport;

/// <summary>
///     Controls how messages are acknowledged when received from the broker. Passed as part of
///     <see cref="MessageBusProcessorOptions" /> when the framework creates a processor.
/// </summary>
public enum MessageBusReceiveMode
{
    /// <summary>
    ///     Messages are locked (made invisible to other consumers) upon receipt and remain in the queue until the handler
    ///     explicitly calls <see cref="IReceivedMessage{TMessage}.Complete" /> or
    ///     <see cref="IReceivedMessage{TMessage}.DeadLetter" />. This is the default mode and is required for at-least-once
    ///     delivery guarantees and dead-letter tracking.
    /// </summary>
    PeekLock,

    /// <summary>
    ///     Messages are removed from the queue as soon as they are received, regardless of whether processing succeeds.
    ///     Calling <see cref="IReceivedMessage{TMessage}.Complete" /> is a no-op in this mode. Use only when at-most-once
    ///     (fire-and-forget) delivery semantics are acceptable and dead-lettering is not required.
    /// </summary>
    ReceiveAndDelete
}