using System.Text.RegularExpressions;
using Azure.Messaging.EventHubs;
using Azure.Storage.Blobs;
using Flowly.MessageInfrastructure.Registration;
using Flowly.Transport;
using Microsoft.Extensions.Logging;

namespace Flowly.AzureEventHubs;

/// <summary>
///     Validates at startup that every stream queue registered against the Azure Event Hubs provider has a matching event
///     hub: the hub exists, it has the partitions the contract declares through <see cref="StreamPartitionsAttribute" />
///     (partition ids <c>0</c> to <c>n-1</c>, or exactly <c>0</c> for a non-partitioned stream), and the partition ownership
///     blob container exists when one is configured. It also reports the stream retention settings a contract declares
///     that Event Hubs does not take from Flowly, and warns when an event hub is geo-replicated while a message stream checkpoint store is
///     registered (<c>isCheckpointStoreRegistered</c>), because Azure documents that the offsets of the same data can differ between regions after a forced failover.
///     <para>
///         The hub side of that warning is exact: <c>IsGeoReplicationEnabled</c> is read for each event hub that has a stream registered on the provider,
///         producer-only hubs included. The checkpoint side is host-wide: <c>isCheckpointStoreRegistered</c> only reports whether any
///         <c>MessageStreamCheckpoint&lt;T&gt;</c> is registered anywhere in the host, not whether one belongs to a message type read from that event hub. It can
///         therefore warn when no Event Hubs position is stored (a checkpoint registered only for a RabbitMQ stream handler, or a hub the host only writes to
///         with <c>IMessageRecorder</c>), and it never misses a checkpoint for a handler that reads a geo-replicated hub, so it errs towards false positives.
///         It is a single startup warning per event hub and never fails startup. Without a registered checkpoint nothing is stored and nothing can go stale.
///     </para>
///     The provider is registered with topology creation forced off so that the
///     framework always runs this validator; there is no reconciliation.
/// </summary>
internal sealed partial class AzureEventHubsTopologyValidator(
    string providerName,
    IEventHubsClientFactory clientFactory,
    BlobContainerClient? partitionOwnershipContainer,
    StreamQueueManifest streamQueueManifest,
    Func<bool> isCheckpointStoreRegistered,
    ILogger<AzureEventHubsTopologyValidator> logger) : IMessagingTopologyValidator
{
    public string ProviderName => providerName;

    public async Task Validate(IReadOnlyCollection<IQueueDescription> queueDescriptions, CancellationToken cancellationToken)
    {
        var streamQueueNames = queueDescriptions
            .Select(queue => queue.Name)
            .Where(streamQueueManifest.IsStreamQueue)
            .ToList();

        if (streamQueueNames.Count == 0)
            return;

        await Task.WhenAll(
            streamQueueNames
                .Select(queueName => ValidateEventHub(queueName, cancellationToken))
                .Append(ValidatePartitionOwnershipContainer(cancellationToken)));
    }

    private async Task ValidateEventHub(string eventHubName, CancellationToken cancellationToken)
    {
        if (!EventHubNameRegex().IsMatch(eventHubName))
            throw new InvalidOperationException(
                $"'{eventHubName}' is not a valid Azure Event Hubs event hub name. The event hub name is the stream queue name resolved for the message contract " +
                $"and must be 1 to 256 characters of letters, digits, '.', '-' or '_', starting and ending with a letter or digit. Use [QueueName] on the message contract to choose a valid name.");

        var declaredPartitionCount = streamQueueManifest.GetPartitionCount(eventHubName);
        var eventHubInfo = await GetEventHubInfo(eventHubName, declaredPartitionCount, cancellationToken);

        if (!EventHubsPartitionIds.Matches(eventHubInfo.PartitionIds, declaredPartitionCount))
            throw new InvalidOperationException(DescribePartitionMismatch(eventHubName, declaredPartitionCount, eventHubInfo.PartitionIds));

        LogRetention(eventHubName);
        LogGeoReplication(eventHubName, eventHubInfo);
    }

    private async Task<EventHubsInfo> GetEventHubInfo(string eventHubName, int? declaredPartitionCount, CancellationToken cancellationToken)
    {
        try
        {
            return await clientFactory.GetInspector(eventHubName).GetEventHubInfo(cancellationToken);
        }
        catch (EventHubsException exception) when (exception.Reason is EventHubsException.FailureReason.ResourceNotFound or EventHubsException.FailureReason.ServiceCommunicationProblem)
        {
            throw new InvalidOperationException(
                $"Azure Event Hubs event hub '{eventHubName}' does not exist or could not be reached, but a message stream is registered for it. Flowly does not create event hubs: " +
                $"create the event hub with {declaredPartitionCount ?? 1} partition(s) before startup, and check that the connection and its permissions allow reading the event hub's properties.",
                exception);
        }
    }

    private async Task ValidatePartitionOwnershipContainer(CancellationToken cancellationToken)
    {
        if (partitionOwnershipContainer is null)
            return;

        if (!await partitionOwnershipContainer.ExistsAsync(cancellationToken))
            throw new InvalidOperationException(
                $"The partition ownership blob container '{partitionOwnershipContainer.Name}' configured for Azure Event Hubs provider '{providerName}' does not exist. " +
                $"Flowly does not create it: create the container before startup.");
    }

    private void LogRetention(string eventHubName)
    {
        if (!streamQueueManifest.TryGetRetention(eventHubName, out var retention))
            return;

        if (retention.MaxLengthBytes is { } maxLengthBytes)
            logger.LogWarning(
                "Stream '{EventHubName}' declares MaxLengthBytes = {MaxLengthBytes}, which Azure Event Hubs does not support. The limit is ignored; Event Hubs retention is time based and configured on the event hub.",
                eventHubName,
                maxLengthBytes);

        if (retention.MaxAgeSeconds is { } maxAgeSeconds)
            logger.LogInformation(
                "Stream '{EventHubName}' declares MaxAgeSeconds = {MaxAgeSeconds}. Azure Event Hubs retention is configured on the event hub, so the value is neither applied nor verified.",
                eventHubName,
                maxAgeSeconds);
    }

    private void LogGeoReplication(string eventHubName, EventHubsInfo eventHubInfo)
    {
        if (!eventHubInfo.IsGeoReplicationEnabled || !isCheckpointStoreRegistered())
            return;

        logger.LogWarning(
            "Event hub '{EventHubName}' is geo-replicated and a message stream checkpoint store is registered. " +
            "Azure documents that after a forced failover the offsets of the same data can differ between regions, " +
            "so a stored position may no longer be valid after such a failover. Flowly does not detect or correct this.",
            eventHubName);
    }

    private static string DescribePartitionMismatch(string eventHubName, int? declaredPartitionCount, IReadOnlyCollection<string> actualPartitionIds)
    {
        var expected = declaredPartitionCount is { } count
            ? $"the message contract declares [StreamPartitions({count})], so the event hub must have the partition ids {string.Join(", ", EventHubsPartitionIds.Expected(count))}"
            : "the message contract is not partitioned, so the event hub must have exactly one partition with the id 0";

        return $"Azure Event Hubs event hub '{eventHubName}' has the partition ids [{string.Join(", ", actualPartitionIds)}], but {expected}. " +
               $"Flowly maps partition ids to partition numbers and does not reconcile the event hub: create the event hub with the declared partition count, " +
               $"or change [StreamPartitions] on the contract to match it.";
    }

    [GeneratedRegex("^[A-Za-z0-9]([A-Za-z0-9._-]{0,254}[A-Za-z0-9])?$")]
    private static partial Regex EventHubNameRegex();
}
