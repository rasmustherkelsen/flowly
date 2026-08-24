using System.Diagnostics;
using Flowly.MessageInfrastructure.Registration;
using Flowly.MessageInfrastructure.Telemetry;

namespace Flowly.MessageInfrastructure.Senders;

internal class MessageSubmitter<TMessage>(
    IMessageBusClientRegistry clientRegistry,
    MessageSubmitter<TMessage>.QueueSettings queueSettings,
    ISubmitterInstrumentation submitterInstrumentation) : IMessageSubmitter<TMessage>
{
    public class QueueSettings(string queueName, string providerName)
    {
        public string QueueName { get; } = queueName;
        public string ProviderName { get; } = providerName;
    }

    public async Task Submit(TMessage message, CancellationToken cancellationToken = default, string? partitionKey = null)
    {
        var sw = Stopwatch.StartNew();
        var client = clientRegistry.GetClient(queueSettings.ProviderName);
        var messageId = Guid.NewGuid().ToString();
        using var activity = submitterInstrumentation.StartSending(queueSettings.QueueName, client.MessagingSystem, messageId);
        activity.ApplyTagsFrom(message);

        try
        {
            var sender = await client.CreateMessageBusSender(queueSettings.QueueName);
            await sender.SendMessage(message, new MessageProperties(messageId, string.Empty, PartitionKey: partitionKey), cancellationToken);
            submitterInstrumentation.RecordSent(queueSettings.QueueName, sw.Elapsed.TotalMilliseconds);
        }
        catch
        {
            submitterInstrumentation.RecordFailed(queueSettings.QueueName);
            throw;
        }
    }
}
