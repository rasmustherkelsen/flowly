using System.Diagnostics;

namespace Flowly.MessageInfrastructure.Telemetry;

internal sealed class NullCallerInstrumentation : ICallerInstrumentation
{
    public Activity? StartCalling(string callQueueName, string messagingSystem, string messageId, string correlationId) => null;

    public void RecordSucceeded(string callQueueName, double durationMs) { }

    public void RecordFailed(string callQueueName) { }

    public Activity? StartReceivingResponse(string replyQueueName, string messagingSystem, ActivityContext parentContext, string messageId, string correlationId) => null;
}
