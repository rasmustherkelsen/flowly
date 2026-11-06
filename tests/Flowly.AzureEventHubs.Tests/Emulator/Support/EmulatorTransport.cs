using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Producer;
using Azure.Storage.Blobs;
using Flowly.MessageInfrastructure.Registration;
using Flowly.Transport;
using Microsoft.Extensions.Logging;

namespace Flowly.AzureEventHubs.Tests.Emulator.Support;

internal sealed record ReceivedEvent<TMessage>(TMessage Body, MessageProperties Properties, int? Partition)
{
    public override string ToString() => $"{Body} @ partition {Partition?.ToString() ?? "-"} offset {Properties.StreamOffset}";
}

internal sealed class EmulatorTransport : IAsyncDisposable
{
    private readonly AzureEventHubsClient _client;
    private readonly EventHubsClientFactory _clientFactory;
    private readonly List<IAsyncDisposable> _disposables = [];
    private readonly Lock _gate = new();

    private EmulatorTransport(AzureEventHubsClient client, EventHubsClientFactory clientFactory)
    {
        _client = client;
        _clientFactory = clientFactory;
    }

    public static EmulatorTransport Create(BlobContainerClient? container = null, TimeSpan? loadBalancingInterval = null, TimeSpan? partitionOwnershipExpiration = null)
    {
        var connectionString = EmulatorEnvironment.RequiredConnectionString;
        var ownershipOptions = EventHubsOwnershipOptions.Create(container, loadBalancingInterval, partitionOwnershipExpiration);
        var manifest = new StreamQueueManifest();

        foreach (var hub in EmulatorEntities.Hubs)
        {
            manifest.MarkAsStream(hub.Name, null, null, hub.ContractPartitions);
        }

        var clientFactory = EventHubsClientFactory.ForConnectionString(connectionString);
        var client = new AzureEventHubsClient(
            clientFactory,
            EventHubsProcessorFactory.ForConnectionString(connectionString, ownershipOptions),
            null,
            manifest,
            ownershipOptions.IsShared,
            TimeProvider.System);

        return new EmulatorTransport(client, clientFactory);
    }

    public async Task Record<TMessage>(
        string hubName,
        TMessage body,
        string? partitionKey = null,
        string? messageId = null,
        string? correlationId = null,
        int retryCount = 0)
    {
        var sender = await _client.CreateStreamSender(hubName);

        await sender.SendMessage(
            body,
            new MessageProperties(messageId ?? Guid.NewGuid().ToString(), correlationId ?? string.Empty, RetryCount: retryCount, PartitionKey: partitionKey));
    }

    public async Task RecordMany<TMessage>(string hubName, string tag, int count, string? partitionKey = null) where TMessage : ITaggedEvent<TMessage>
    {
        foreach (var number in Enumerable.Range(0, count))
        {
            await Record(hubName, TMessage.Create(tag, number), partitionKey);
        }
    }

    public async Task<StreamSubscription<TMessage>> Subscribe<TMessage>(string hubName, string consumerGroup, StartPosition startPosition)
    {
        var processor = await _client.CreateStreamProcessor<TMessage>(hubName, startPosition, new MessageBusProcessorOptions(100, MessageBusReceiveMode.PeekLock) { ConsumerName = consumerGroup });
        var subscription = new StreamSubscription<TMessage>(processor);
        Track(subscription);

        await subscription.Start();

        return subscription;
    }

    public async Task<PartitionedSubscription<TMessage>> SubscribePartitioned<TMessage>(
        string hubName,
        string consumerGroup,
        int partitionCount,
        Func<int, CancellationToken, Task<StartPosition>> resolveStartPosition,
        ILogger? logger = null,
        bool start = true)
    {
        var consumer = await _client.CreatePartitionedStreamConsumer<TMessage>(
            hubName,
            partitionCount,
            resolveStartPosition,
            new MessageBusProcessorOptions(100, MessageBusReceiveMode.PeekLock) { ConsumerName = consumerGroup },
            logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);
        var subscription = new PartitionedSubscription<TMessage>(consumer);
        Track(subscription);

        if (start)
            await subscription.Start();

        return subscription;
    }

    public Task<long?> ResolveStartOffset(string hubName, int? partition, StartPosition startPosition)
        => _client.ResolveStartOffset(hubName, partition, startPosition, CancellationToken.None);

    public Task<EventHubsInfo> EventHubInfo(string hubName) => _clientFactory.GetInspector(hubName).GetEventHubInfo(CancellationToken.None);

    public Task<EventHubsPartitionState> PartitionState(string hubName, int partition) => _clientFactory.GetInspector(hubName).GetPartitionState(partition.ToString(), CancellationToken.None);

    public async ValueTask DisposeAsync()
    {
        List<IAsyncDisposable> disposables;

        lock (_gate)
        {
            disposables = [.. _disposables];
            _disposables.Clear();
        }

        foreach (var disposable in disposables)
        {
            await disposable.DisposeAsync();
        }

        await _client.DisposeAsync();
    }

    private void Track(IAsyncDisposable disposable)
    {
        lock (_gate)
        {
            _disposables.Add(disposable);
        }
    }

    public static async Task SendToPartition<TMessage>(string hubName, int partition, params TMessage[] bodies)
    {
        await using var producer = new EventHubProducerClient(EmulatorEnvironment.RequiredConnectionString, hubName);
        using var batch = await producer.CreateBatchAsync(new CreateBatchOptions { PartitionId = partition.ToString() });

        foreach (var body in bodies)
        {
            if (!batch.TryAdd(new EventData(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(body))))
                throw new InvalidOperationException("The event does not fit the batch.");
        }

        await producer.SendAsync(batch);
    }
}

internal sealed class StreamSubscription<TMessage>(IMessageBusProcessor<TMessage> processor) : IAsyncDisposable
{
    public Observed<ReceivedEvent<TMessage>> Received { get; } = new();

    public Observed<ErrorDetails> Errors { get; } = new();

    public async Task Start()
    {
        processor.ProcessMessage += (message, _) =>
        {
            Received.Add(new ReceivedEvent<TMessage>(message.Body, message.Properties, null));

            return Task.CompletedTask;
        };
        processor.ProcessError += error =>
        {
            Errors.Add(error);

            return Task.CompletedTask;
        };

        await processor.StartProcessingMessages();
    }

    public async ValueTask DisposeAsync()
    {
        await processor.StopProcessing(CancellationToken.None);
        await processor.DisposeAsync();
    }
}

internal sealed class PartitionedSubscription<TMessage>(IPartitionedStreamConsumer<TMessage> consumer) : IAsyncDisposable
{
    public Observed<ReceivedEvent<TMessage>> Received { get; } = new();

    public Observed<ErrorDetails> Errors { get; } = new();

    public Observed<int> Assigned { get; } = new();

    public Observed<int> Revoked { get; } = new();

    public Task Start()
    {
        consumer.PartitionAssigned += (partition, processor) =>
        {
            processor.ProcessMessage += (message, _) =>
            {
                Received.Add(new ReceivedEvent<TMessage>(message.Body, message.Properties, partition));

                return Task.CompletedTask;
            };
            processor.ProcessError += error =>
            {
                Errors.Add(error);

                return Task.CompletedTask;
            };

            Assigned.Add(partition);

            return Task.CompletedTask;
        };
        consumer.PartitionRevoked += partition =>
        {
            Revoked.Add(partition);

            return Task.CompletedTask;
        };

        return consumer.StartProcessingMessages();
    }

    public async ValueTask DisposeAsync()
    {
        await consumer.StopProcessing(CancellationToken.None);
        await consumer.DisposeAsync();
    }
}
