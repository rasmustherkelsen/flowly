using Flowly.Transport;

namespace Flowly.RabbitMQ;

internal static class RabbitMqCustomProperties
{
    private const string FirstDeathPrefix = "x-first-death-";
    private const string LastDeathPrefix = "x-last-death-";

    public static IReadOnlyDictionary<string, string> Read(IDictionary<string, object?>? headers)
        => headers is null
            ? CustomMessageProperties.Empty
            : CustomMessageProperties.Read(headers.Where(header => !IsBrokerDeathHeader(header.Key)));

    private static bool IsBrokerDeathHeader(string key)
        => key.StartsWith(FirstDeathPrefix, StringComparison.Ordinal) || key.StartsWith(LastDeathPrefix, StringComparison.Ordinal);
}
