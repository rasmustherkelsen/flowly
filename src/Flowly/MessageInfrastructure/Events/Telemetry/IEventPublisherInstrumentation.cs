using System.Diagnostics;

namespace Flowly.MessageInfrastructure.Events.Telemetry;

internal interface IEventPublisherInstrumentation
{
    bool IsEnabled { get; }

    Activity? StartRaising(string topicName, string messagingSystem, string messageId);

    void RecordRaised(string topicName, double durationMs);

    void RecordFailed(string topicName);
}
