using System.Diagnostics;
using Flowly.MessageInfrastructure.StreamPartitions;

namespace Flowly.MessageInfrastructure.Telemetry;

internal sealed class NullHandlerInstrumentation : IHandlerInstrumentation
{
    public bool IsEnabled => false;

    public Activity? StartHandling(string handlerName, string queueName, string messagingSystem, MessageProperties messageProperties, ActivityContext parentContext = default)
        => null;

    public Activity? StartHandling(string handlerName, string queueName, string messagingSystem, MessageProperties messageProperties, IEnumerable<ActivityLink> links)
        => null;

    public void RecordReceived(string handlerName, string queueName, long count = 1) { }

    public void RecordSucceeded(string handlerName, string queueName, double durationMs, long count = 1) { }

    public void RecordFailed(string handlerName, string queueName, long count = 1) { }

    public void RecordRetried(string handlerName, string queueName, long count = 1) { }

    public void RecordHalted(string handlerName, string queueName, string reason) { }

    public void RecordStreamPartitionTransition(string handlerName, string queueName, string consumerName, int? partition, StreamPartitionState state) { }

    public void RecordCheckpointSaved(string handlerName, string queueName, double durationMs) { }

    public void RecordCheckpointSaveFailed(string handlerName, string queueName) { }

    public void RecordCheckpointResolved(string handlerName, string queueName, CheckpointResolution resolution) { }

    public Activity? StartSendingResponse(string callQueueName, string replyQueueName, string messagingSystem, string messageId, string correlationId) => null;

    public void RecordResponseSent(string callQueueName, double durationMs) { }

    public void RecordResponseFailed(string callQueueName) { }
}
