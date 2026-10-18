using System.Diagnostics;
using Flowly.MessageInfrastructure.Registration;
using Flowly.MessageInfrastructure.Telemetry;
using Flowly.Transport;

namespace Flowly.MessageInfrastructure.Senders;

internal class MessageSubmitter<TMessage>(
    IMessageBusClientRegistry clientRegistry,
    MessageSubmitter<TMessage>.QueueSettings queueSettings,
    ISubmitterInstrumentation submitterInstrumentation) : IMessageSubmitter<TMessage>
{
    public class QueueSettings(string queueName, string providerName, bool isStream = false)
    {
        public string QueueName { get; } = queueName;
        public string ProviderName { get; } = providerName;
        public bool IsStream { get; } = isStream;
    }

    public async Task Submit(TMessage message, CancellationToken cancellationToken = default, string? partitionKey = null)
    {
        var sw = Stopwatch.StartNew();
        var client = clientRegistry.GetTransportClient(queueSettings.ProviderName);
        var messageId = Guid.NewGuid().ToString();
        using var activity = submitterInstrumentation.StartSending(queueSettings.QueueName, client.MessagingSystem, messageId);
        activity.ApplyTagsFrom(message);

        try
        {
            var sender = await CreateSender(client);
            await sender.SendMessage(message, new MessageProperties(messageId, string.Empty, PartitionKey: partitionKey), cancellationToken);
            submitterInstrumentation.RecordSent(queueSettings.QueueName, sw.Elapsed.TotalMilliseconds);
        }
        catch
        {
            submitterInstrumentation.RecordFailed(queueSettings.QueueName);
            throw;
        }
    }

    private Task<IMessageBusSender> CreateSender(ITransportClient client) => queueSettings.IsStream
        ? client.Require<IStreamCapableMessageBusClient>(queueSettings.ProviderName).CreateStreamSender(queueSettings.QueueName)
        : client.Require<IMessageBusClient>(queueSettings.ProviderName).CreateMessageBusSender(queueSettings.QueueName);
}
