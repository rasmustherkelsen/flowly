using System.Diagnostics;

namespace Flowly.MessageInfrastructure.Events.Telemetry;

public interface IEventPublisherInstrumentation
{
    bool IsEnabled { get; }

    Activity? StartRaising(string topicOrExchangeName, string messagingSystem, string messageId);

    void RecordRaised(string topicOrExchangeName, double durationMs);

    void RecordFailed(string topicOrExchangeName);
}
