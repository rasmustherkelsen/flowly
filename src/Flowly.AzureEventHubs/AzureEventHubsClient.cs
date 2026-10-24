using System.Collections.Concurrent;
using Flowly.MessageInfrastructure.Registration;
using Flowly.Transport;
using Microsoft.Extensions.Logging;

namespace Flowly.AzureEventHubs;

internal sealed class AzureEventHubsClient(
    IEventHubsClientFactory clientFactory,
    long? maxMessageSizeBytes,
    StreamQueueManifest streamQueueManifest) : IStreamCapableMessageBusClient, IPartitionedStreamCapableMessageBusClient, IAsyncDisposable
{
    private const string ConsumptionNotSupportedMessage = "Consuming streams from Azure Event Hubs is not supported by this version of Flowly.AzureEventHubs. Azure Event Hubs can currently only be used to record messages with IMessageRecorder.";

    private readonly ConcurrentDictionary<string, IMessageBusSender> _senders = new(StringComparer.Ordinal);

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
        => Task.FromException<IMessageBusProcessor<TMessage>>(new NotSupportedException(ConsumptionNotSupportedMessage));

    public Task<IPartitionedStreamConsumer<TMessage>> CreatePartitionedStreamConsumer<TMessage>(
        string queueName,
        int partitionCount,
        Func<int, CancellationToken, Task<StartPosition>> resolveStartPosition,
        MessageBusProcessorOptions options,
        ILogger logger)
        => Task.FromException<IPartitionedStreamConsumer<TMessage>>(new NotSupportedException(ConsumptionNotSupportedMessage));

    public ValueTask DisposeAsync() => clientFactory.DisposeAsync();
}
