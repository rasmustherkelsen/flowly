using System.Text;
using System.Text.Json;
using Flowly.Transport;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Flowly.RabbitMQ;

internal class RabbitMqReceivedMessage<TMessage>(IChannel channel, BasicDeliverEventArgs args, bool autoAck = false) : IReceivedMessage<TMessage>
{
    private TMessage? _body;

    public TMessage Body => _body
        ??= JsonSerializer.Deserialize<TMessage>(args.Body.Span)
            ?? throw new InvalidOperationException($"Deserialized message body is null for type {typeof(TMessage).FullName}.");

    public MessageProperties Properties { get; } = new(
        args.BasicProperties.MessageId ?? string.Empty,
        args.BasicProperties.CorrelationId ?? string.Empty,
        RetryCount: GetRetryCount(args.BasicProperties),
        Traceparent: GetStringHeader(args.BasicProperties, "traceparent"),
        Tracestate: GetStringHeader(args.BasicProperties, "tracestate"),
        ReplyTo: string.IsNullOrEmpty(args.BasicProperties.ReplyTo) ? null : args.BasicProperties.ReplyTo);

    public Task Complete(CancellationToken cancellationToken = default)
    {
        if (autoAck) return Task.CompletedTask;
        return channel.BasicAckAsync(args.DeliveryTag, false, cancellationToken).AsTask();
    }

    public Task DeadLetter(string? reason = null, CancellationToken cancellationToken = default)
    {
        if (autoAck) return Task.CompletedTask;
        return channel.BasicNackAsync(args.DeliveryTag, false, false, cancellationToken).AsTask();
    }

    private static int GetRetryCount(IReadOnlyBasicProperties properties)
    {
        if (properties.Headers is null) return 0;
        if (!properties.Headers.TryGetValue(FlowlyMessageProperties.RetryCount, out var rc)) return 0;
        return rc switch
        {
            int i => i,
            long l => (int)l,
            byte[] b => int.TryParse(Encoding.UTF8.GetString(b), out var parsed) ? parsed : 0,
            _ => 0
        };
    }

    private static string? GetStringHeader(IReadOnlyBasicProperties properties, string key)
    {
        if (properties.Headers is null) return null;
        if (!properties.Headers.TryGetValue(key, out var value)) return null;

        return value switch
        {
            string s => s,
            byte[] b => Encoding.UTF8.GetString(b),
            _ => null
        };
    }
}

internal class RabbitMqUntypedReceivedMessage(BasicDeliverEventArgs args) : IReceivedMessage
{
    public TBody GetBody<TBody>()
    {
        return JsonSerializer.Deserialize<TBody>(args.Body.Span)
               ?? throw new InvalidOperationException($"Deserialized message body is null for type {typeof(TBody).FullName}.");
    }

    public MessageProperties Properties { get; } = new(
        args.BasicProperties.MessageId ?? string.Empty,
        args.BasicProperties.CorrelationId ?? string.Empty);
}