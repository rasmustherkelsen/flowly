namespace Flowly.Transport;

/// <summary>
///     Optional extension of <see cref="IMessageBusClient" /> for transport providers that support pub/sub topics
///     (Azure Service Bus topics, RabbitMQ fanout exchanges, etc.). When your <see cref="IMessageBusClient" />
///     implementation also implements this interface, Flowly will use it to wire up <see cref="IEventSender" /> and
///     <see cref="EventHandlerBase{TEvent}" /> registrations. If your broker only supports point-to-point queues you do
///     not need to implement this interface.
/// </summary>
public interface IEventCapableMessageBusClient
{
    /// <summary>
    ///     Creates a sender that publishes events to the specified topic or exchange. The framework calls this method for
    ///     every <see cref="IEventSender" /> registration so that events can be fanned out to all active subscriptions.
    /// </summary>
    /// <param name="topicName">The name of the topic or exchange to publish events to.</param>
    /// <returns>An <see cref="IMessageBusSender" /> targeting the specified topic or exchange.</returns>
    Task<IMessageBusSender> CreateEventPublisher(string topicName);

    /// <summary>
    ///     Creates a push-based processor for the specified subscription on a topic or exchange. The framework calls this
    ///     method for every <see cref="EventHandlerBase{TEvent}" /> registration so that the event handler background
    ///     service can receive events delivered to that subscription.
    /// </summary>
    /// <param name="topicName">The name of the topic or exchange.</param>
    /// <param name="subscriptionName">The name of the subscription (Azure Service Bus) or per-handler queue (RabbitMQ).</param>
    /// <param name="options">Concurrency and receive-mode settings for this processor.</param>
    /// <typeparam name="TEvent">The expected event body type; used to deserialise the raw payload.</typeparam>
    /// <returns>An <see cref="IMessageBusProcessor{TMessage}" /> that has not yet been started.</returns>
    Task<IMessageBusProcessor<TEvent>> CreateEventProcessor<TEvent>(
        string topicName,
        string subscriptionName,
        MessageBusProcessorOptions options);

    /// <summary>
    ///     Creates a sender used to re-publish a dead-lettered event back onto the topic so that it is routed to the
    ///     originating subscription only. Implementations should set the <c>flowly-target-subscription</c> application
    ///     property (see <see cref="FlowlyMessageProperties.TargetSubscription" />) and configure the subscription filter
    ///     accordingly. Called by the dead-letter requeue operation.
    /// </summary>
    /// <param name="topicName">The name of the topic or exchange.</param>
    /// <param name="subscriptionName">The subscription that should receive the re-published event.</param>
    /// <returns>An <see cref="IMessageBusSender" /> for sending the retry message to the correct subscription.</returns>
    Task<IMessageBusSender> CreateEventRetrySender(string topicName, string subscriptionName);

    /// <summary>
    ///     Creates a receiver for the dead-letter sub-queue (or equivalent) of the specified subscription. The framework
    ///     calls this method when dead-letter tracking is enabled for an event handler, so that a background service can
    ///     drain and persist dead-lettered events.
    /// </summary>
    /// <param name="topicName">The name of the topic or exchange.</param>
    /// <param name="subscriptionName">The name of the subscription whose dead letters should be drained.</param>
    /// <returns>An <see cref="IDeadLetterReceiver" /> for the subscription's dead-letter sub-queue.</returns>
    Task<IDeadLetterReceiver> CreateEventSubscriptionDeadLetterReceiver(string topicName, string subscriptionName);

    /// <summary>
    ///     Returns the number of messages currently sitting in the dead-letter sub-queue of the specified subscription.
    ///     Used by telemetry gauges to surface per-subscription dead-letter queue depth as a metric.
    /// </summary>
    /// <param name="topicName">The name of the topic or exchange.</param>
    /// <param name="subscriptionName">The name of the subscription whose dead-letter depth should be measured.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>The number of messages in the subscription's dead-letter sub-queue.</returns>
    Task<long> GetEventSubscriptionDeadLetterMessageCount(string topicName, string subscriptionName, CancellationToken cancellationToken = default);
}