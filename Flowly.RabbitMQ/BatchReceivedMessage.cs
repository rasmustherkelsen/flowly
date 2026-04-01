using System.Text.Json;
using Flowly.MessagingAbstractions;
using RabbitMQ.Client;

namespace Flowly.RabbitMQ;

internal class RabbitMqBatchReceivedMessage<TMessage>(IChannel channel, ulong deliveryTag, ReadOnlyMemory<byte> body, IReadOnlyBasicProperties properties) : IReceivedMessage<TMessage>
{
    private TMessage? _body;

    public TMessage Body => _body
        ??= JsonSerializer.Deserialize<TMessage>(body.Span)
        ?? throw new InvalidOperationException($"Deserialized message body is null for type {typeof(TMessage).FullName}.");

    public MessageProperties Properties { get; } = new(
        properties.MessageId ?? string.Empty,
        properties.CorrelationId ?? string.Empty);

    public Task Complete(CancellationToken cancellationToken = default)
        => channel.BasicAckAsync(deliveryTag, multiple: false, cancellationToken).AsTask();

    public Task DeadLetter(string? reason = null, CancellationToken cancellationToken = default)
        => channel.BasicNackAsync(deliveryTag, multiple: false, requeue: false, cancellationToken).AsTask();
}
