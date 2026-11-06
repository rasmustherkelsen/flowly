using System.Collections.Concurrent;
using Flowly.MessageInfrastructure.Registration;
using Flowly.Transport;
using Microsoft.Extensions.Logging;

namespace Flowly.AzureEventHubs;

internal sealed class AzureEventHubsClient(
    IEventHubsClientFactory clientFactory,
    IEventHubsProcessorFactory processorFactory,
    long? maxMessageSizeBytes,
    StreamQueueManifest streamQueueManifest,
    bool sharesPartitionOwnership,
    TimeProvider timeProvider) : IPartitionedStreamCapableMessageBusClient, IStreamStartOffsetResolver, IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, IMessageBusSender> _senders = new(StringComparer.Ordinal);
    private readonly EventHubsStartOffsetResolver _startOffsetResolver = new(clientFactory);

    public string MessagingSystem => "azure-event-hubs";

    public Task<IMessageBusSender> CreateStreamSender(string queueName)
        => Task.FromResult(_senders.GetOrAdd(
            queueName,
            eventHubName => new EventHubsStreamSender(
                eventHubName,
                clientFactory.GetProducer(eventHubName),
                maxMessageSizeBytes,
                streamQueueManifest.GetPartitionCount(eventHubName) is not null)));

    public Task<IMessageBusProcessor<TMessage>> CreateStreamProcessor<TMessage>(string queueName, StartPosition startPosition, MessageBusProcessorOptions options)
        => Task.FromResult<IMessageBusProcessor<TMessage>>(new EventHubsStreamProcessor<TMessage>(
            processorFactory,
            queueName,
            EventHubsConsumerGroup.FromConsumerName(queueName, options.ConsumerName),
            startPosition,
            sharesPartitionOwnership,
            timeProvider));

    public Task<IPartitionedStreamConsumer<TMessage>> CreatePartitionedStreamConsumer<TMessage>(
        string queueName,
        int partitionCount,
        Func<int, CancellationToken, Task<StartPosition>> resolveStartPosition,
        MessageBusProcessorOptions options,
        ILogger logger)
        => Task.FromResult<IPartitionedStreamConsumer<TMessage>>(new EventHubsPartitionedStreamConsumer<TMessage>(
            processorFactory,
            queueName,
            EventHubsConsumerGroup.FromConsumerName(queueName, options.ConsumerName),
            partitionCount,
            resolveStartPosition,
            sharesPartitionOwnership,
            logger,
            timeProvider));

    public Task<long?> ResolveStartOffset(string queueName, int? partition, StartPosition startPosition, CancellationToken cancellationToken)
        => _startOffsetResolver.ResolveStartOffset(queueName, partition, startPosition, cancellationToken);

    public ValueTask DisposeAsync() => clientFactory.DisposeAsync();
}
