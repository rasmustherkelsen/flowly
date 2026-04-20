using System.Text;
using Flowly.MessagingAbstractions;
using RabbitMQ.Client;

namespace Flowly.RabbitMQ;

internal class RabbitMqDeadLetterReceivedMessage : IDeadLetterMessage
{
    internal ulong DeliveryTag { get; }

    public string MessageId { get; }
    public string RawBody { get; }
    public IReadOnlyDictionary<string, object> ApplicationProperties { get; }
    public string? DeadLetterReason { get; }
    public string? DeadLetterErrorDescription { get; }
    public DateTimeOffset EnqueuedTime { get; }

    public RabbitMqDeadLetterReceivedMessage(ulong deliveryTag, ReadOnlyMemory<byte> body, IReadOnlyBasicProperties properties)
    {
        DeliveryTag = deliveryTag;
        MessageId = properties.MessageId ?? string.Empty;
        RawBody = Encoding.UTF8.GetString(body.Span);

        var appProps = new Dictionary<string, object>();
        if (properties.Headers is not null)
        {
            foreach (var (key, value) in properties.Headers)
            {
                if (key.StartsWith("x-", StringComparison.Ordinal)) continue;
                appProps[key] = value is byte[] bytes ? Encoding.UTF8.GetString(bytes) : value ?? string.Empty;
            }
        }
        ApplicationProperties = appProps;

        (DeadLetterReason, EnqueuedTime) = ExtractDeathInfo(properties);
    }

    private static (string? reason, DateTimeOffset time) ExtractDeathInfo(IReadOnlyBasicProperties properties)
    {
        if (properties.Headers is null) return (null, DateTimeOffset.UtcNow);
        if (!properties.Headers.TryGetValue("x-death", out var xDeath)) return (null, DateTimeOffset.UtcNow);
        if (xDeath is not IList<object> deathList || deathList.Count == 0) return (null, DateTimeOffset.UtcNow);
        if (deathList[0] is not IDictionary<string, object> death) return (null, DateTimeOffset.UtcNow);

        string? reason = null;
        if (death.TryGetValue("reason", out var r))
            reason = r is byte[] rb ? Encoding.UTF8.GetString(rb) : r?.ToString();

        DateTimeOffset time = DateTimeOffset.UtcNow;
        if (death.TryGetValue("time", out var t))
        {
            time = t switch
            {
                AmqpTimestamp ts => DateTimeOffset.FromUnixTimeSeconds(ts.UnixTime),
                long l => DateTimeOffset.FromUnixTimeSeconds(l),
                _ => DateTimeOffset.UtcNow
            };
        }

        return (reason, time);
    }
}
