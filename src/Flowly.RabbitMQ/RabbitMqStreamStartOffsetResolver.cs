using Flowly.Transport;

namespace Flowly.RabbitMQ;

internal sealed class RabbitMqStreamStartOffsetResolver(IStreamOffsetProbe streamOffsetProbe) : IStreamStartOffsetResolver
{
    public Task<long?> ResolveStartOffset(string queueName, int? partition, StartPosition startPosition, CancellationToken cancellationToken)
    {
        var streamName = partition is { } partitionIndex ? RabbitMqMessagingTopologyCreator.PartitionStreamName(queueName, partitionIndex) : queueName;

        return startPosition.Match(
            first: () => Task.FromResult<long?>(0),
            last: () => ResolveLast(streamName, cancellationToken),
            offset: offset => Task.FromResult<long?>(Math.Max(offset, 0)),
            timestamp: timestamp => ResolveTimestamp(streamName, timestamp, cancellationToken));
    }

    private async Task<long?> ResolveLast(string streamName, CancellationToken cancellationToken)
    {
        var probeResult = await streamOffsetProbe.ProbeLastChunk(streamName, cancellationToken);

        return probeResult.Status switch
        {
            StreamProbeStatus.Delivered => probeResult.LastOffset + 1,
            StreamProbeStatus.Empty => 0,
            _ => null
        };
    }

    private async Task<long?> ResolveTimestamp(string streamName, DateTime timestamp, CancellationToken cancellationToken)
    {
        var probeResult = await streamOffsetProbe.ProbeFromTimestamp(streamName, timestamp, cancellationToken);

        return probeResult.Status switch
        {
            StreamProbeStatus.Delivered => probeResult.FirstOffset,
            StreamProbeStatus.Empty => await ResolveEmptyStreamToZero(streamName, cancellationToken),
            _ => null
        };
    }

    private async Task<long?> ResolveEmptyStreamToZero(string streamName, CancellationToken cancellationToken)
    {
        var lastChunkProbeResult = await streamOffsetProbe.ProbeLastChunk(streamName, cancellationToken);

        return lastChunkProbeResult.Status == StreamProbeStatus.Empty ? 0 : null;
    }
}
