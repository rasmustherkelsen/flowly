using System.Diagnostics;

namespace Flowly.MessageInfrastructure.Events.Telemetry;

internal sealed class NullEventPublisherInstrumentation : IEventPublisherInstrumentation
{
    public bool IsEnabled => false;

    public Activity? StartRaising(string topicOrExchangeName, string messagingSystem, string messageId)
        => null;

    public void RecordRaised(string topicOrExchangeName, double durationMs) { }

    public void RecordFailed(string topicOrExchangeName) { }
}
