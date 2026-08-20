using Flowly.Transport;

namespace Flowly.InMemory;

/// <summary>
///     InMemory's <see cref="IPartitionedStreamConsumer{TMessage}" />. Ownership is trivial — InMemory is
///     single-process by construction, so this process owns every partition immediately and permanently;
///     <see cref="PartitionRevoked" /> is never raised. Exists for parity with the partitioned API surface and as a
///     free, no-infra reference implementation for developing and testing partition-aware handler code, not for any
///     cross-instance scale-out benefit — that's not something a single process can offer regardless of transport.
/// </summary>
internal sealed class InMemoryPartitionedStreamConsumer<TMessage>(
    InMemoryBroker broker,
    string queueName,
    int partitionCount,
    Func<int, CancellationToken, Task<StartPosition>> resolveStartPosition) : IPartitionedStreamConsumer<TMessage>
{
    private readonly List<InMemoryStreamProcessor<TMessage>> _processors = [];

    public event Func<int, IMessageBusProcessor<TMessage>, Task>? PartitionAssigned;

    public event Func<int, Task>? PartitionRevoked
    {
        add { }
        remove { }
    }

    public async Task StartProcessingMessages(CancellationToken cancellationToken = default)
    {
        for (var partition = 0; partition < partitionCount; partition++)
        {
            var log = broker.GetOrCreatePartitionedStreamLog(queueName, partition);
            var startPosition = await resolveStartPosition(partition, cancellationToken);
            var startOffset = log.ResolveStartOffset(startPosition);
            var processor = new InMemoryStreamProcessor<TMessage>(log, startOffset, $"{queueName}::{partition}");

            _processors.Add(processor);

            // Fire PartitionAssigned before starting the processor — the subscriber (StreamPartitionRunner) must
            // hook ProcessMessage first, or messages already in the log could be dispatched before anyone is
            // listening. Mirrors the non-partitioned CreateStreamProcessor contract: return unstarted, let the
            // caller subscribe, then start.
            if (PartitionAssigned != null)
                await PartitionAssigned(partition, processor);
        }
    }

    public async Task StopProcessing(CancellationToken cancellationToken)
    {
        foreach (var processor in _processors)
            await processor.StopProcessing(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var processor in _processors)
            await processor.DisposeAsync();
    }
}
