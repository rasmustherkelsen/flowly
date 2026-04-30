using Microsoft.Extensions.Logging;

namespace Flowly.Transport;

/// <summary>
///     Defines the per-message handling strategy used by message processing background services. Flowly ships two
///     built-in implementations: <c>StandardMessageHandlingStrategy</c> for regular handlers and
///     <c>JobMessageHandlingStrategy</c> for job handlers that require state tracking. Implement this interface to
///     introduce custom cross-cutting behaviour (e.g. a custom retry mechanism or audit trail) for a specific message
///     type.
/// </summary>
/// <typeparam name="TMessage">The message type this strategy handles. Must be a reference type.</typeparam>
public interface IMessageHandlingStrategy<TMessage> where TMessage : class
{
    /// <summary>
    ///     Executes the main message handling logic for the supplied <paramref name="receivedMessage" />. The background
    ///     service calls this method for every message dispatched by <see cref="IMessageBusProcessor{TMessage}" />. On
    ///     success, the implementation is responsible for completing the message via
    ///     <see cref="IReceivedMessage{TMessage}.Complete" />. On transient failure the implementation should
    ///     re-publish the message with an updated retry count (or dead-letter it if retries are exhausted) and then
    ///     complete the original message so that it does not re-appear on the queue.
    /// </summary>
    /// <param name="receivedMessage">The message received from the transport.</param>
    /// <param name="serviceProvider">A scoped <see cref="IServiceProvider" /> for this message's processing scope.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to abort processing.</param>
    Task HandleMessage(IReceivedMessage<TMessage> receivedMessage, IServiceProvider serviceProvider, CancellationToken cancellationToken);

    /// <summary>
    ///     Called by the background service after all retry attempts have been exhausted. Implementations should perform
    ///     any final cleanup (e.g. mark a job as failed, write a telemetry event) and then dead-letter or complete the
    ///     message.
    /// </summary>
    /// <param name="receivedMessage">The message that could not be processed after all retries.</param>
    /// <param name="exception">The last exception thrown during message handling.</param>
    /// <param name="serviceProvider">A scoped <see cref="IServiceProvider" /> for this message's processing scope.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to abort the operation.</param>
    Task OnRetriesExhausted(IReceivedMessage<TMessage> receivedMessage, Exception exception, IServiceProvider serviceProvider, CancellationToken cancellationToken);

    /// <summary>
    ///     Called by the background service when a broker-level <see cref="ErrorDetails" /> event is received from
    ///     <see cref="IMessageBusProcessor{TMessage}.ProcessError" />. Implementations should log the error and perform
    ///     any necessary recovery actions.
    /// </summary>
    /// <param name="logger">The logger to use for recording the error.</param>
    /// <param name="serviceProvider">A scoped <see cref="IServiceProvider" /> available at the time of the error.</param>
    /// <param name="errorDetails">Details of the broker-level error, including the exception and endpoint.</param>
    Task OnMessageHandlingError(ILogger logger, IServiceProvider serviceProvider, ErrorDetails errorDetails);
}