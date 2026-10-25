using Azure.Core;
using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Primitives;
using Azure.Messaging.EventHubs.Processor;

namespace Flowly.AzureEventHubs;

internal sealed class EventHubsSdkProcessor : IEventHubsProcessor
{
    private const int EventBatchMaximumCount = 100;
    private const string StartupProbePartitionId = "-1";

    private readonly FlowlyEventProcessor _processor;
    private readonly string _eventHubName;
    private readonly string _consumerGroup;

    private EventHubsSdkProcessor(FlowlyEventProcessor processor, string eventHubName, string consumerGroup)
    {
        _processor = processor;
        _eventHubName = eventHubName;
        _consumerGroup = consumerGroup;
    }

    public static EventHubsSdkProcessor ForConnectionString(string connectionString, EventHubsProcessorRequest request)
        => new(new FlowlyEventProcessor(connectionString, request), request.EventHubName, request.ConsumerGroup);

    public static EventHubsSdkProcessor ForCredential(string fullyQualifiedNamespace, TokenCredential credential, EventHubsProcessorRequest request)
        => new(new FlowlyEventProcessor(fullyQualifiedNamespace, credential, request), request.EventHubName, request.ConsumerGroup);

    public async Task StartProcessing(CancellationToken cancellationToken)
    {
        try
        {
            await _processor.StartProcessingAsync(cancellationToken);
        }
        catch (Exception exception) when (IsMissingResource(exception))
        {
            throw new InvalidOperationException(DescribeMissingConsumerGroup(_eventHubName, _consumerGroup), exception);
        }
    }

    public Task StopProcessing(CancellationToken cancellationToken) => _processor.StopProcessingAsync(cancellationToken);

    internal static async Task<EventProcessorCheckpoint?> ResolveCheckpoint(
        IEventHubsPartitionCallbacks callbacks,
        string fullyQualifiedNamespace,
        string eventHubName,
        string consumerGroup,
        string partitionId,
        CancellationToken cancellationToken)
    {
        if (partitionId == StartupProbePartitionId)
            return null;

        return new EventProcessorCheckpoint
        {
            FullyQualifiedNamespace = fullyQualifiedNamespace,
            EventHubName = eventHubName,
            ConsumerGroup = consumerGroup,
            PartitionId = partitionId,
            StartingPosition = await callbacks.GetStartingPosition(partitionId, cancellationToken)
        };
    }

    internal static bool IsMissingResource(Exception exception)
        => exception switch
        {
            EventHubsException { Reason: EventHubsException.FailureReason.ResourceNotFound } => true,
            AggregateException aggregateException => aggregateException.Flatten().InnerExceptions.Any(IsMissingResource),
            _ => false
        };

    internal static string DescribeMissingConsumerGroup(string eventHubName, string consumerGroup)
        => $"Azure Event Hubs consumer group '{consumerGroup}' does not exist on event hub '{eventHubName}'. " +
           "Flowly reads an event hub through the consumer group named by the stream handler's ConsumerName, which defaults to the handler type name: " +
           "create the consumer group on the event hub, or set ConsumerName on the handler (\"$Default\" selects the default consumer group).";

    private sealed class FlowlyEventProcessor : PluggableCheckpointStoreEventProcessor<EventProcessorPartition>
    {
        private readonly IEventHubsPartitionCallbacks _callbacks;

        public FlowlyEventProcessor(string connectionString, EventHubsProcessorRequest request)
            : base(request.Store, EventBatchMaximumCount, request.ConsumerGroup, connectionString, request.EventHubName, request.Options)
            => _callbacks = request.Callbacks;

        public FlowlyEventProcessor(string fullyQualifiedNamespace, TokenCredential credential, EventHubsProcessorRequest request)
            : base(request.Store, EventBatchMaximumCount, request.ConsumerGroup, fullyQualifiedNamespace, request.EventHubName, credential, request.Options)
            => _callbacks = request.Callbacks;

        protected override Task<EventProcessorCheckpoint?> GetCheckpointAsync(string partitionId, CancellationToken cancellationToken)
            => ResolveCheckpoint(_callbacks, FullyQualifiedNamespace, EventHubName, ConsumerGroup, partitionId, cancellationToken);

        protected override Task OnInitializingPartitionAsync(EventProcessorPartition partition, CancellationToken cancellationToken)
            => _callbacks.InitializePartition(partition.PartitionId, cancellationToken);

        protected override Task OnPartitionProcessingStoppedAsync(EventProcessorPartition partition, ProcessingStoppedReason reason, CancellationToken cancellationToken)
            => _callbacks.StopPartition(partition.PartitionId, reason, cancellationToken);

        protected override Task OnProcessingEventBatchAsync(IEnumerable<EventData> events, EventProcessorPartition partition, CancellationToken cancellationToken)
            => _callbacks.ProcessEvents(partition.PartitionId, events as IReadOnlyList<EventData> ?? [.. events], cancellationToken);

        protected override Task OnProcessingErrorAsync(Exception exception, EventProcessorPartition? partition, string operationDescription, CancellationToken cancellationToken)
            => _callbacks.HandleError(exception, partition?.PartitionId, operationDescription, cancellationToken);
    }
}
