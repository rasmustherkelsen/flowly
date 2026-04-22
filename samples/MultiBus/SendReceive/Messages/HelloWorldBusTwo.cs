using Flowly.MessageInfrastructure.Receivers;

namespace Messages;

[ProviderAffinity("RabbitMQ")]
public record HelloWorldBusTwo(string Payload);