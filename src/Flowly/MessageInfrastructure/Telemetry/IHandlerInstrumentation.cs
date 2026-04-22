using System.Diagnostics;

namespace Flowly.MessageInfrastructure.Telemetry;

public interface IHandlerInstrumentation
{
    bool IsEnabled { get; }

    Activity? StartHandling(
        string handlerName,
        string queueName,
        string messagingSystem,
        MessageProperties messageProperties,
        ActivityContext parentContext = default);

    void RecordReceived(string handlerName, string queueName, long count = 1);

    void RecordSucceeded(string handlerName, string queueName, double durationMs, long count = 1);

    void RecordFailed(string handlerName, string queueName, long count = 1);

    void RecordRetried(string handlerName, string queueName);
}
