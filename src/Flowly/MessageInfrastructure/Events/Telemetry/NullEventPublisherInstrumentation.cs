using System.Diagnostics;

namespace Flowly.MessageInfrastructure.Events.Telemetry;

internal sealed class NullEventPublisherInstrumentation : IEventPublisherInstrumentation
{
    public bool IsEnabled => false;

    public Activity? StartRaising(string topicName, string messagingSystem, string messageId)
        => null;

    public void RecordRaised(string topicName, double durationMs) { }

    public void RecordFailed(string topicName) { }
}
