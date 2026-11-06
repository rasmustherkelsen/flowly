using RabbitMQ.Client;

namespace Flowly.RabbitMQ;

internal static class AmqpStreamOffset
{
    public static AmqpTimestamp Timestamp(DateTime timestamp)
    {
        var utcTimestamp = timestamp.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(timestamp, DateTimeKind.Utc)
            : timestamp.ToUniversalTime();

        return new AmqpTimestamp(new DateTimeOffset(utcTimestamp).ToUnixTimeSeconds());
    }
}
