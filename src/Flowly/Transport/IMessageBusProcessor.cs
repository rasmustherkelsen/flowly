namespace Flowly.Transport;

/// <summary>
///     Push-based message processor for a single queue or event subscription. The framework subscribes to
///     <see cref="ProcessMessage" /> and <see cref="ProcessError" /> immediately after calling
///     <see cref="IMessageBusClient.CreateProcessor{TMessage}" />, then calls
///     <see cref="StartProcessingMessages" /> when the hosted background service starts. Implementations must
///     honour <see cref="MessageBusProcessorOptions.MaxConcurrentCalls" /> and dispatch all incoming messages to the
///     registered delegates.
/// </summary>
/// <typeparam name="TMessage">The expected message body type.</typeparam>
public interface IMessageBusProcessor<TMessage> : IAsyncDisposable
{
    /// <summary>
    ///     Raised by the implementation for each message received from the queue or subscription. The framework handler
    ///     deserialises the body, executes the registered <see cref="MessageHandler{TMessage}" />, and then calls
    ///     <see cref="IReceivedMessage{TMessage}.Complete" /> or <see cref="IReceivedMessage{TMessage}.DeadLetter" />
    ///     according to the outcome.
    /// </summary>
    event Func<IReceivedMessage<TMessage>, CancellationToken, Task> ProcessMessage;

    /// <summary>
    ///     Raised by the implementation when a broker-level error occurs (e.g. connection loss). The framework logs the
    ///     error and, depending on the transport, may attempt to reconnect or stop the processor.
    /// </summary>
    event Func<ErrorDetails, Task> ProcessError;

    /// <summary>
    ///     Begins receiving and dispatching messages. Called once by the background service during application start.
    ///     Implementations should block (or start an internal loop) and keep delivering messages to the delegates until
    ///     <see cref="StopProcessing" /> is called.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token that can be used to stop processing.</param>
    Task StartProcessingMessages(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gracefully stops the processor. Called by the background service during application shutdown. After this
    ///     method completes no further <see cref="ProcessMessage" /> events should be raised.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token that limits the time allowed for a graceful stop.</param>
    Task StopProcessing(CancellationToken cancellationToken);
}