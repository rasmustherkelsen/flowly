using System.Diagnostics;
using System.Threading.Channels;
using Flowly.MessageInfrastructure;
using Flowly.MessageInfrastructure.StreamPartitions;
using Flowly.MessageInfrastructure.Telemetry;

namespace Flowly.Tests.Fakes;

internal sealed class RecordingHandlerInstrumentation : IHandlerInstrumentation
{
    private readonly Lock _lock = new();
    private readonly List<StreamPartitionTransition> _transitions = [];
    private readonly Channel<string> _halts = Channel.CreateUnbounded<string>();
    private readonly List<long> _receivedCounts = [];
    private long _failedCount;
    private long _retriedCount;

    public bool IsEnabled => false;

    public long FailedCount => Interlocked.Read(ref _failedCount);

    public long RetriedCount => Interlocked.Read(ref _retriedCount);

    public IReadOnlyList<StreamPartitionTransition> Transitions
    {
        get
        {
            lock (_lock)
            {
                return [.._transitions];
            }
        }
    }

    public IReadOnlyList<long> ReceivedCounts
    {
        get
        {
            lock (_lock)
            {
                return [.._receivedCounts];
            }
        }
    }

    public async Task<string> NextHalt() => await _halts.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));

    public Activity? StartHandling(string handlerName, string queueName, string messagingSystem, MessageProperties messageProperties, ActivityContext parentContext = default) => null;

    public Activity? StartHandling(string handlerName, string queueName, string messagingSystem, MessageProperties messageProperties, IEnumerable<ActivityLink> links) => null;

    public void RecordReceived(string handlerName, string queueName, long count = 1)
    {
        lock (_lock)
        {
            _receivedCounts.Add(count);
        }
    }

    public void RecordSucceeded(string handlerName, string queueName, double durationMs, long count = 1) { }

    public void RecordFailed(string handlerName, string queueName, long count = 1) => Interlocked.Add(ref _failedCount, count);

    public void RecordRetried(string handlerName, string queueName, long count = 1) => Interlocked.Add(ref _retriedCount, count);

    public void RecordHalted(string handlerName, string queueName, string reason) => _halts.Writer.TryWrite(reason);

    public void RecordStreamPartitionTransition(string handlerName, string queueName, string consumerName, int? partition, StreamPartitionState state)
    {
        lock (_lock)
        {
            _transitions.Add(new StreamPartitionTransition(handlerName, queueName, consumerName, partition, state));
        }
    }

    public void RecordCheckpointSaved(string handlerName, string queueName, double durationMs) { }

    public void RecordCheckpointSaveFailed(string handlerName, string queueName) { }

    public void RecordCheckpointResolved(string handlerName, string queueName, CheckpointResolution resolution) { }

    public Activity? StartSendingResponse(string callQueueName, string replyQueueName, string messagingSystem, string messageId, string correlationId) => null;

    public void RecordResponseSent(string callQueueName, double durationMs) { }

    public void RecordResponseFailed(string callQueueName) { }
}

internal sealed record StreamPartitionTransition(string HandlerName, string QueueName, string ConsumerName, int? Partition, StreamPartitionState State);
