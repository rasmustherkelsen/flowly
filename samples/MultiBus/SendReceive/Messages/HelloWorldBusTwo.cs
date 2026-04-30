using Flowly;

namespace Messages;

[ProviderAffinity("RabbitMQ")]
public record HelloWorldBusTwo(string Payload);