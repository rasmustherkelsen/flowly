using Azure.Messaging.ServiceBus;
using Flowly.MessagingAbstractions;

namespace Flowly.AzureServiceBus;

internal class DeadLetterReceivedMessage(ServiceBusReceivedMessage message) : IDeadLetterMessage
{
    public string MessageId { get; } = message.MessageId;
    
    public string RawBody { get; } = message.Body.ToString();
    
    public IReadOnlyDictionary<string, object> ApplicationProperties { get; } = message.ApplicationProperties;
    
    public string? DeadLetterReason { get; } = message.DeadLetterReason;
    
    public string? DeadLetterErrorDescription { get; } = message.DeadLetterErrorDescription;
    
    public DateTimeOffset EnqueuedTime { get; } = message.EnqueuedTime;

    internal ServiceBusReceivedMessage ServiceBusReceivedMessage { get; } = message;
}
