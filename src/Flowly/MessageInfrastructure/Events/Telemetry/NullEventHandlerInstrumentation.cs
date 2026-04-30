using System.Diagnostics;

namespace Flowly.MessageInfrastructure.Events.Telemetry;

internal sealed class NullEventHandlerInstrumentation : IEventHandlerInstrumentation
{
    public bool IsEnabled => false;

    public Activity? StartHandling(string handlerName, string topicName, string messagingSystem, MessageProperties messageProperties, ActivityContext parentContext = default)
        => null;

    public void RecordReceived(string handlerName, string topicName, long count = 1) { }

    public void RecordSucceeded(string handlerName, string topicName, double durationMs, long count = 1) { }

    public void RecordFailed(string handlerName, string topicName, long count = 1) { }

    public void RecordRetried(string handlerName, string topicName) { }
}
