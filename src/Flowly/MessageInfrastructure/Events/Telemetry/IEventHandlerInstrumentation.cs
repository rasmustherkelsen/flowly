using System.Diagnostics;

namespace Flowly.MessageInfrastructure.Events.Telemetry;

internal interface IEventHandlerInstrumentation
{
    bool IsEnabled { get; }

    Activity? StartHandling(
        string handlerName,
        string topicOrExchangeName,
        string messagingSystem,
        MessageProperties messageProperties,
        ActivityContext parentContext = default);

    void RecordReceived(string handlerName, string topicOrExchangeName, long count = 1);

    void RecordSucceeded(string handlerName, string topicOrExchangeName, double durationMs, long count = 1);

    void RecordFailed(string handlerName, string topicOrExchangeName, long count = 1);

    void RecordRetried(string handlerName, string topicOrExchangeName);
}
