namespace Flowly.Transport;

/// <summary>
///     Session-scoped push-based processor used exclusively by the recurring job infrastructure. Created by
///     <see cref="IMessageBusClient.CreateExecutionLaneProcessor" />, it delivers only messages whose session ID (or
///     equivalent lane identifier) matches the registered recurring job, ensuring that at most one instance of a
///     recurring job runs at any given time. Structurally similar to <see cref="IMessageBusProcessor{TMessage}" />
///     but receives un-typed <see cref="IReceivedMessage" /> payloads because the body format varies (empty trigger
///     messages carry no payload).
/// </summary>
public interface IExecutionLaneProcessor : IAsyncDisposable
{
    /// <summary>
    ///     Raised by the implementation for each message whose lane/session matches the registered filter. The framework
    ///     handler executes the recurring job and then completes or abandons the message.
    /// </summary>
    event Func<IReceivedMessage, CancellationToken, Task> ProcessMessage;

    /// <summary>
    ///     Raised by the implementation when a broker-level error occurs. The framework logs the error and may stop the
    ///     processor.
    /// </summary>
    event Func<ErrorDetails, Task> ProcessError;

    /// <summary>
    ///     Begins receiving and dispatching messages for the registered lane. Called once by the recurring job background
    ///     service during application start.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token that can be used to stop processing.</param>
    Task StartProcessing(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gracefully stops the processor. Called by the recurring job background service during application shutdown.
    ///     After this method completes no further <see cref="ProcessMessage" /> events should be raised.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token that limits the time allowed for a graceful stop.</param>
    Task StopProcessing(CancellationToken cancellationToken = default);
}