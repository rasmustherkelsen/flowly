using Azure.Messaging.ServiceBus;
using Flowly.Transport;

namespace Flowly.AzureServiceBus;

internal class DeadLetterReceivedMessage(ServiceBusReceivedMessage message) : IDeadLetterMessage
{
    internal ServiceBusReceivedMessage ServiceBusReceivedMessage { get; } = message;
    public string MessageId { get; } = message.MessageId;

    public string RawBody => ServiceBusReceivedMessage.Body.ToString();

    public IReadOnlyDictionary<string, object> ApplicationProperties { get; } = message.ApplicationProperties;

    public string? DeadLetterReason { get; } = message.DeadLetterReason;

    public string? DeadLetterErrorDescription { get; } = message.DeadLetterErrorDescription;

    public DateTimeOffset EnqueuedTime { get; } = message.EnqueuedTime;
}