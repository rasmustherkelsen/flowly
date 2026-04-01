using System.Text;
using System.Text.Json;
using Flowly.MessagingAbstractions;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Flowly.RabbitMQ;

internal class RabbitMqReceivedMessage<TMessage>(IChannel channel, BasicDeliverEventArgs args) : IReceivedMessage<TMessage>
{
    private TMessage? _body;

    public TMessage Body => _body
        ??= JsonSerializer.Deserialize<TMessage>(args.Body.Span)
        ?? throw new InvalidOperationException($"Deserialized message body is null for type {typeof(TMessage).FullName}.");

    public MessageProperties Properties { get; } = new(
        args.BasicProperties.MessageId ?? string.Empty,
        args.BasicProperties.CorrelationId ?? string.Empty,
        RetryCount: GetRetryCount(args.BasicProperties));

    public Task Complete(CancellationToken cancellationToken = default)
        => channel.BasicAckAsync(args.DeliveryTag, multiple: false, cancellationToken).AsTask();

    public Task DeadLetter(string? reason = null, CancellationToken cancellationToken = default)
        => channel.BasicNackAsync(args.DeliveryTag, multiple: false, requeue: false, cancellationToken).AsTask();

    private static int GetRetryCount(IReadOnlyBasicProperties properties)
    {
        if (properties.Headers is null) return 0;
        if (!properties.Headers.TryGetValue("flowly-retry-count", out var rc)) return 0;
        return rc switch
        {
            int i => i,
            long l => (int)l,
            byte[] b => int.TryParse(Encoding.UTF8.GetString(b), out var parsed) ? parsed : 0,
            _ => 0
        };
    }
}

internal class RabbitMqUntypedReceivedMessage(BasicDeliverEventArgs args) : IReceivedMessage
{
    public TBody GetBody<TBody>()
        => JsonSerializer.Deserialize<TBody>(args.Body.Span)
        ?? throw new InvalidOperationException($"Deserialized message body is null for type {typeof(TBody).FullName}.");

    public MessageProperties Properties { get; } = new(
        args.BasicProperties.MessageId ?? string.Empty,
        args.BasicProperties.CorrelationId ?? string.Empty);
}
