using Azure.Messaging.ServiceBus;
using Flowly.Transport;

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
        RetryCount: args.Message.ApplicationProperties.TryGetValue(FlowlyMessageProperties.RetryCount, out var rc) ? Convert.ToInt32(rc) : 0,
        Traceparent: args.Message.ApplicationProperties.TryGetValue("traceparent", out var tp) ? tp as string : null,
        Tracestate: args.Message.ApplicationProperties.TryGetValue("tracestate", out var ts) ? ts as string : null);

    public Task Complete(CancellationToken cancellationToken = default)
    {
        return args.CompleteMessageAsync(args.Message, cancellationToken);
    }

    public Task DeadLetter(string? reason = null, CancellationToken cancellationToken = default)
    {
        return args.DeadLetterMessageAsync(args.Message, reason, cancellationToken: cancellationToken);
    }
}

internal class ReceivedMessage(ServiceBusReceivedMessage serviceBusReceivedMessage) : IReceivedMessage
{
    public TBody GetBody<TBody>()
    {
        return serviceBusReceivedMessage.Body.ToObjectFromJson<TBody>()!;
    }

    public MessageProperties Properties { get; } = new(serviceBusReceivedMessage.MessageId, serviceBusReceivedMessage.CorrelationId);
}