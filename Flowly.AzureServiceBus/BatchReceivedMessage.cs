using Azure.Messaging.ServiceBus;
using Flowly.MessagingAbstractions;

namespace Flowly.AzureServiceBus;

internal class BatchReceivedMessage<TMessage>(ServiceBusReceiver receiver, ServiceBusReceivedMessage message) : IReceivedMessage<TMessage>
{
    public TMessage Body => field
        ??= message.Body.ToObjectFromJson<TMessage>()
            ?? throw new InvalidOperationException($"Deserialized message body is null for type {typeof(TMessage).FullName}.");

    public MessageProperties Properties { get; } = new(
        message.MessageId,
        message.CorrelationId,
        RetryCount: message.ApplicationProperties.TryGetValue("flowly-retry-count", out var rc) ? Convert.ToInt32(rc) : 0);

    public Task Complete(CancellationToken cancellationToken = default)
        => receiver.CompleteMessageAsync(message, cancellationToken);

    public Task DeadLetter(string? reason = null, CancellationToken cancellationToken = default)
        => receiver.DeadLetterMessageAsync(message, deadLetterReason: reason, cancellationToken: cancellationToken);
}