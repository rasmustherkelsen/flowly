using System.Diagnostics;
using Flowly.MessagingAbstractions;

namespace Flowly.MessageInfrastructure.Events.Telemetry;

internal sealed class NullEventHandlerInstrumentation : IEventHandlerInstrumentation
{
    public bool IsEnabled => false;

    public Activity? StartHandling(string handlerName, string topicOrExchangeName, string messagingSystem, MessageProperties messageProperties, ActivityContext parentContext = default)
        => null;

    public void RecordReceived(string handlerName, string topicOrExchangeName, long count = 1) { }

    public void RecordSucceeded(string handlerName, string topicOrExchangeName, double durationMs, long count = 1) { }

    public void RecordFailed(string handlerName, string topicOrExchangeName, long count = 1) { }

    public void RecordRetried(string handlerName, string topicOrExchangeName) { }
}
