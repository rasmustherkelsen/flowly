using System.Diagnostics;
using Flowly.MessageInfrastructure.Telemetry;
using Flowly.MessagingAbstractions;

namespace Flowly.MessageInfrastructure.Senders;

public class MessageSubmitter<TMessage>(
    IMessageBusClient messageBusClient,
    MessageSubmitter<TMessage>.QueueSettings queueSettings,
    SubmitterInstrumentation submitterInstrumentation) : IMessageSubmitter<TMessage>
{
    public class QueueSettings(string queueName)
    {
        public string QueueName { get; } = queueName;
    }

    public async Task Submit(TMessage message, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        using var activity = submitterInstrumentation.StartSending(queueSettings.QueueName);
        try
        {
            var sender = await messageBusClient.CreateMessageBusSender(queueSettings.QueueName);
            await sender.SendMessage(message, MessageProperties.Empty, cancellationToken);
            submitterInstrumentation.RecordSent(queueSettings.QueueName, sw.Elapsed.TotalMilliseconds);
        }
        catch
        {
            submitterInstrumentation.RecordFailed(queueSettings.QueueName);
            throw;
        }
    }
}
