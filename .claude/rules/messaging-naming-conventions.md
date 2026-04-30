# Messaging Naming Conventions

## Topic vs Exchange

Use `TopicName` as the transport-agnostic term for the pub/sub destination — not `TopicOrExchangeName`.

This applies to:
- Interface method parameters (e.g. `IEventCapableMessageBusClient`)
- Internal records and settings classes (e.g. `EventHandlerSettings`, `DeferredEventRegistration`)
- Local variables

Each transport adapter is the correct place to map `topicName` to the broker-specific concept:
- Azure Service Bus → Topic
- RabbitMQ → Exchange (map locally: `var exchangeName = topicName`)

"Topic" is the dominant term across the messaging ecosystem (Kafka, AWS SNS, Google Cloud Pub/Sub, ActiveMQ, Azure Service Bus). RabbitMQ is the only outlier, and the adaptation belongs in the RabbitMQ transport layer, not in the shared abstractions.
