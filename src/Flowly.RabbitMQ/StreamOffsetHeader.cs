using System.Text;
using RabbitMQ.Client;

namespace Flowly.RabbitMQ;

internal static class StreamOffsetHeader
{
    public const string Name = "x-stream-offset";

    public static long? Read(IReadOnlyBasicProperties properties)
    {
        if (properties.Headers is null) return null;
        if (!properties.Headers.TryGetValue(Name, out var offset)) return null;

        return offset switch
        {
            long l => l,
            int i => i,
            byte[] b => long.TryParse(Encoding.UTF8.GetString(b), out var parsed) ? parsed : null,
            _ => null
        };
    }
}
