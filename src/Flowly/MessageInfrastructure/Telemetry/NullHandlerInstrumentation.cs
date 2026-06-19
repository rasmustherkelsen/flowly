using System.Diagnostics;

namespace Flowly.MessageInfrastructure.Telemetry;

internal sealed class NullHandlerInstrumentation : IHandlerInstrumentation
{
    public bool IsEnabled => false;

    public Activity? StartHandling(string handlerName, string queueName, string messagingSystem, MessageProperties messageProperties, ActivityContext parentContext = default)
        => null;

    public void RecordReceived(string handlerName, string queueName, long count = 1) { }

    public void RecordSucceeded(string handlerName, string queueName, double durationMs, long count = 1) { }

    public void RecordFailed(string handlerName, string queueName, long count = 1) { }

    public void RecordRetried(string handlerName, string queueName, long count = 1) { }
}
