using System.Diagnostics;
using Flowly.MessageInfrastructure.Registration;
using Flowly.MessageInfrastructure.Telemetry;
using Flowly.Transport;

namespace Flowly.MessageInfrastructure.Senders;

internal class MessageSubmitter<TMessage>(
    IMessageBusClientRegistry clientRegistry,
    string queueName,
    string providerName,
    ISubmitterInstrumentation submitterInstrumentation,
    bool isStream = false) : IMessageSubmitter<TMessage>
{
    public async Task Submit(TMessage message, CancellationToken cancellationToken = default, string? partitionKey = null)
    {
        var sw = Stopwatch.StartNew();
        var client = clientRegistry.GetTransportClient(providerName);
        var messageId = Guid.NewGuid().ToString();
        using var activity = submitterInstrumentation.StartSending(queueName, client.MessagingSystem, messageId);
        activity.ApplyTagsFrom(message);

        try
        {
            var sender = await CreateSender(client);
            await sender.SendMessage(message, new MessageProperties(messageId, string.Empty, PartitionKey: partitionKey), cancellationToken);
            submitterInstrumentation.RecordSent(queueName, sw.Elapsed.TotalMilliseconds);
        }
        catch
        {
            submitterInstrumentation.RecordFailed(queueName);
            throw;
        }
    }

    private Task<IMessageBusSender> CreateSender(ITransportClient client) => isStream
        ? client.Require<IStreamCapableMessageBusClient>(providerName).CreateStreamSender(queueName)
        : client.Require<IMessageBusClient>(providerName).CreateMessageBusSender(queueName);
}
