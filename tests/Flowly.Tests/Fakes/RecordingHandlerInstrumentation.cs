using System.Diagnostics;
using Flowly.MessageInfrastructure;
using Flowly.MessageInfrastructure.StreamPartitions;
using Flowly.MessageInfrastructure.Telemetry;

namespace Flowly.Tests.Fakes;

internal sealed class RecordingHandlerInstrumentation : IHandlerInstrumentation
{
    private readonly Lock _lock = new();
    private readonly List<StreamPartitionTransition> _transitions = [];

    public bool IsEnabled => false;

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

    public Activity? StartHandling(string handlerName, string queueName, string messagingSystem, MessageProperties messageProperties, ActivityContext parentContext = default) => null;

    public Activity? StartHandling(string handlerName, string queueName, string messagingSystem, MessageProperties messageProperties, IEnumerable<ActivityLink> links) => null;

    public void RecordReceived(string handlerName, string queueName, long count = 1) { }

    public void RecordSucceeded(string handlerName, string queueName, double durationMs, long count = 1) { }

    public void RecordFailed(string handlerName, string queueName, long count = 1) { }

    public void RecordRetried(string handlerName, string queueName, long count = 1) { }

    public void RecordHalted(string handlerName, string queueName, string reason) { }

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
