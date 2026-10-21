using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Flowly.RabbitMQ;

internal sealed record RabbitMqStreamOffsetProbeTimings(
    TimeSpan AttachTimeout,
    TimeSpan QuietWindow,
    TimeSpan MaxCollectionTime,
    ushort Prefetch,
    int AcknowledgeEvery)
{
    public static RabbitMqStreamOffsetProbeTimings Default { get; } = new(TimeSpan.FromSeconds(1), TimeSpan.FromMilliseconds(250), TimeSpan.FromSeconds(1), 1000, 250);
}

/// <summary>
///     Probes a stream over plain AMQP, on a dedicated short-lived channel of the shared consumer connection, so no Stream
///     protocol port is needed. The consumer is cancelled and its channel closed before the probe returns, whatever the outcome.
///     <para>
///         A stream that delivers nothing within <see cref="RabbitMqStreamOffsetProbeTimings.AttachTimeout" /> after the consumer
///         attached is reported as empty only when the broker also reports no messages for it; a stream the broker reports as
///         holding messages that delivers nothing is reported as inconclusive. Delivery is cut off after
///         <see cref="RabbitMqStreamOffsetProbeTimings.MaxCollectionTime" /> on a stream that keeps receiving messages, which
///         can only make the highest observed offset lower than the real tail, never higher.
///     </para>
/// </summary>
internal sealed class RabbitMqStreamOffsetProbe(IRabbitMqConnectionPool connectionPool, RabbitMqStreamOffsetProbeTimings? timings = null) : IStreamOffsetProbe
{
    private readonly RabbitMqStreamOffsetProbeTimings _timings = timings ?? RabbitMqStreamOffsetProbeTimings.Default;

    public Task<StreamProbeResult> ProbeLastChunk(string streamName, CancellationToken cancellationToken)
        => Probe(streamName, "last", cancellationToken);

    public Task<StreamProbeResult> ProbeFromTimestamp(string streamName, DateTime timestamp, CancellationToken cancellationToken)
        => Probe(streamName, AmqpStreamOffset.Timestamp(timestamp), cancellationToken);

    private async Task<StreamProbeResult> Probe(string streamName, object offsetSpecification, CancellationToken cancellationToken)
    {
        var connection = await connectionPool.GetConsumerConnection(cancellationToken);
        var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
        var collector = new StreamProbeDeliveryCollector(channel, _timings.AcknowledgeEvery);
        string? consumerTag = null;

        try
        {
            await channel.BasicQosAsync(0, _timings.Prefetch, false, cancellationToken);

            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += collector.OnDelivered;

            consumerTag = await channel.BasicConsumeAsync(
                streamName,
                false,
                string.Empty,
                false,
                false,
                new Dictionary<string, object?> { [StreamOffsetHeader.Name] = offsetSpecification },
                consumer,
                cancellationToken);

            if (!await collector.WaitForFirstDelivery(_timings.AttachTimeout, cancellationToken))
                return await ClassifyWithoutDelivery(channel, streamName, cancellationToken);

            await collector.WaitUntilQuiet(_timings.QuietWindow, _timings.MaxCollectionTime, cancellationToken);

            return StreamProbeResult.Delivered(collector.FirstOffset!.Value, collector.LastOffset!.Value);
        }
        finally
        {
            await Release(channel, collector, consumerTag);
        }
    }

    private static async Task<StreamProbeResult> ClassifyWithoutDelivery(IChannel channel, string streamName, CancellationToken cancellationToken)
        => await channel.MessageCountAsync(streamName, cancellationToken) == 0 ? StreamProbeResult.Empty : StreamProbeResult.Inconclusive;

    private static async Task Release(IChannel channel, StreamProbeDeliveryCollector collector, string? consumerTag)
    {
        await collector.Stop();
        await TryCancelAndClose(channel, consumerTag);
    }

    private static async Task TryCancelAndClose(IChannel channel, string? consumerTag)
    {
        try
        {
            if (consumerTag is not null)
                await channel.BasicCancelAsync(consumerTag);

            await channel.CloseAsync();
        }
        catch (Exception)
        {
        }
        finally
        {
            await channel.DisposeAsync();
        }
    }
}
