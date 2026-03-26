using Azure.Messaging.ServiceBus;
using Flowly.MessagingAbstractions;

namespace Flowly.AzureServiceBus;

internal class ReceivedMessage<TMessage>(ProcessMessageEventArgs args) : IReceivedMessage<TMessage>
{
    private TMessage? _body = default;

    public TMessage Body => _body
        ??= args.Message.Body.ToObjectFromJson<TMessage>()
        ?? throw new InvalidOperationException($"Deserialized message body is null for type {typeof(TMessage).FullName}.");

    public MessageProperties Properties { get; } = new(
        args.Message.MessageId,
        args.Message.CorrelationId,
        RetryCount: args.Message.ApplicationProperties.TryGetValue("flowly-retry-count", out var rc) ? Convert.ToInt32(rc) : 0);

    public Task Complete(CancellationToken cancellationToken = default)
        => args.CompleteMessageAsync(args.Message, cancellationToken);

    public Task DeadLetter(string? reason = null, CancellationToken cancellationToken = default)
        => args.DeadLetterMessageAsync(args.Message, deadLetterReason: reason, cancellationToken: cancellationToken);
}

internal class ReceivedMessage(ServiceBusReceivedMessage serviceBusReceivedMessage) : IReceivedMessage
{
    public TBody GetBody<TBody>() => serviceBusReceivedMessage.Body.ToObjectFromJson<TBody>()!;

    public MessageProperties Properties { get; } = new(serviceBusReceivedMessage.MessageId, serviceBusReceivedMessage.CorrelationId);
}
