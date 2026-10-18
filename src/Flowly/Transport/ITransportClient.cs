namespace Flowly.Transport;

/// <summary>
///     The root of every transport client. A transport provider registers a client that implements this interface plus one
///     or more capability interfaces describing what the underlying broker can do:
///     <see cref="IMessageBusClient" /> for queue-based messaging (optionally with
///     <see cref="IEventCapableMessageBusClient" /> for pub/sub events) and
///     <see cref="IStreamCapableMessageBusClient" /> for append-only, replayable streams. A transport may implement any
///     combination — Azure Service Bus implements the queue and event capabilities, a stream-only transport implements only
///     the stream capability, and RabbitMQ implements all of them. Flowly resolves the capability a registration needs from
///     the client registry and fails with a descriptive error when the provider does not support it.
/// </summary>
public interface ITransportClient
{
    /// <summary>
    ///     A human-readable identifier for the underlying messaging technology (e.g. <c>"azure-service-bus"</c> or
    ///     <c>"rabbitmq"</c>). Used by telemetry and logging to tag spans and metrics with the messaging system name.
    /// </summary>
    string MessagingSystem { get; }
}
