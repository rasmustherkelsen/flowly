namespace Flowly.Transport;

/// <summary>
///     Core factory interface that every Flowly transport provider must implement. The framework resolves this interface
///     from the DI container (keyed by provider name) and calls its methods to obtain the primitives needed for each
///     registered handler, submitter, and background service. Implement this interface — and optionally
///     <see cref="IEventCapableMessageBusClient" /> when your broker supports pub/sub topics — to create a new transport
///     provider.
/// </summary>
public interface IMessageBusClient
{
    /// <summary>
    ///     A human-readable identifier for the underlying messaging technology (e.g. <c>"azure-service-bus"</c> or
    ///     <c>"rabbitmq"</c>). Used by telemetry and logging to tag spans and metrics with the messaging system name.
    /// </summary>
    string MessagingSystem { get; }

    /// <summary>
    ///     Creates a pull-based receiver for the specified queue. The framework calls this method for
    ///     <see cref="BatchMessageHandler{TMessage}" /> registrations so that the background service can poll for a batch
    ///     of messages, process them, and then acknowledge the whole batch at once.
    /// </summary>
    /// <param name="queueName">The name of the queue to receive messages from.</param>
    /// <returns>An <see cref="IMessageBusReceiver" /> ready to receive messages from the queue.</returns>
    Task<IMessageBusReceiver> CreateReceiver(string queueName);

    /// <summary>
    ///     Creates a push-based processor for the specified queue. The framework calls this method for
    ///     <see cref="MessageHandler{TMessage}" /> and
    ///     <see>
    ///         <cref>Flowly.Jobs.JobHandler{TMessage}</cref>
    ///     </see>
    ///     registrations.
    ///     The processor raises <see cref="IMessageBusProcessor{TMessage}.ProcessMessage" /> for every incoming message
    ///     and must honour <see cref="MessageBusProcessorOptions.MaxConcurrentCalls" />.
    /// </summary>
    /// <param name="queueName">The name of the queue to process messages from.</param>
    /// <param name="options">Concurrency and receive-mode settings for this processor.</param>
    /// <typeparam name="TMessage">The expected message body type; used to deserialise the raw payload.</typeparam>
    /// <returns>An <see cref="IMessageBusProcessor{TMessage}" /> that has not yet been started.</returns>
    Task<IMessageBusProcessor<TMessage>> CreateProcessor<TMessage>(string queueName, MessageBusProcessorOptions options);

    /// <summary>
    ///     Creates a session-scoped processor that filters messages to a single execution lane. The framework calls this
    ///     method for <see>
    ///         <cref>Flowly.Jobs.RecurringJobHandler</cref>
    ///     </see>
    ///     registrations to ensure that only one instance of a
    ///     given recurring job runs at a time. Implementations should restrict delivery to the session whose ID matches
    ///     <paramref name="laneFilter" />.
    /// </summary>
    /// <param name="queueName">The name of the session-enabled queue.</param>
    /// <param name="laneFilter">The session/lane identifier to exclusively process.</param>
    /// <param name="options">Concurrency and receive-mode settings for this processor.</param>
    /// <returns>An <see cref="IExecutionLaneProcessor" /> that has not yet been started.</returns>
    Task<IExecutionLaneProcessor> CreateExecutionLaneProcessor(string queueName, string laneFilter, MessageBusProcessorOptions options);

    /// <summary>
    ///     Creates a sender for publishing messages to the specified queue. The framework calls this method for every
    ///     <see cref="IMessageSender" /> and
    ///     <see>
    ///         <cref>Flowly.Jobs.IJobMessageSender</cref>
    ///     </see>
    ///     registration, as well as for
    ///     internal retry and state-tracking messages.
    /// </summary>
    /// <param name="queueName">The name of the queue to send messages to.</param>
    /// <returns>An <see cref="IMessageBusSender" /> targeting the specified queue.</returns>
    Task<IMessageBusSender> CreateMessageBusSender(string queueName);

    /// <summary>
    ///     Creates a receiver for the dead-letter sub-queue of the specified queue. The framework calls this method for
    ///     handlers that opt in to dead-letter tracking via <c>.WithDeadLetterTracking()</c>, so that a background service
    ///     can drain dead-lettered messages and persist them to the database.
    /// </summary>
    /// <param name="queueName">The name of the queue whose dead-letter sub-queue should be accessed.</param>
    /// <returns>An <see cref="IDeadLetterReceiver" /> for the dead-letter sub-queue.</returns>
    Task<IDeadLetterReceiver> CreateDeadLetterReceiver(string queueName);

    /// <summary>
    ///     Returns the number of messages currently sitting in the dead-letter sub-queue of the specified queue. Used by
    ///     telemetry gauges to surface dead-letter queue depth as a metric.
    /// </summary>
    /// <param name="queueName">The name of the queue whose dead-letter depth should be measured.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>The number of messages in the dead-letter sub-queue.</returns>
    Task<long> GetDeadLetterMessageCount(string queueName, CancellationToken cancellationToken = default);
}