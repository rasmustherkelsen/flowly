using System.Diagnostics;

namespace Flowly.MessageInfrastructure.Telemetry;

public interface ISubmitterInstrumentation
{
    bool IsEnabled { get; }

    Activity? StartSending(string queueName, string messagingSystem, string messageId);

    void RecordSent(string queueName, double durationMs);

    void RecordFailed(string queueName);
}
