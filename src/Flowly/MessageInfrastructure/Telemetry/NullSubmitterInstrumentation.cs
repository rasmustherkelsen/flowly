using System.Diagnostics;

namespace Flowly.MessageInfrastructure.Telemetry;

internal sealed class NullSubmitterInstrumentation : ISubmitterInstrumentation
{
    public bool IsEnabled => false;

    public Activity? StartSending(string queueName, string messagingSystem, string messageId) => null;

    public void RecordSent(string queueName, double durationMs) { }

    public void RecordFailed(string queueName) { }
}
