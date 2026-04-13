using Azure.Messaging.ServiceBus;
using Flowly.MessagingAbstractions;

namespace Flowly.AzureServiceBus;

internal class ReceivedMessage<TMessage>(ProcessMessageEventArgs args) : IReceivedMessage<TMessage>
{
    public TMessage Body
    {
        get => field ??= args.Message.Body.ToObjectFromJson<TMessage>() ?? throw new InvalidOperationException($"Deserialized message body is null for type {typeof(TMessage).FullName}.");
    } = default;

    public MessageProperties Properties { get; } = new(
        args.Message.MessageId,
        args.Message.CorrelationId,
        RetryCount: args.Message.ApplicationProperties.TryGetValue("flowly-retry-count", out var rc) ? Convert.ToInt32(rc) : 0,
        Traceparent: args.Message.ApplicationProperties.TryGetValue("traceparent", out var tp) ? tp as string : null,
        Tracestate: args.Message.ApplicationProperties.TryGetValue("tracestate", out var ts) ? ts as string : null);

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
