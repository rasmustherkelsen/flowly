using Flowly.Transport;

namespace Flowly.MessageInfrastructure.Registration;

internal static class TransportClientCapability
{
    public static TClient Require<TClient>(this ITransportClient client, string providerName)
        where TClient : class
        => client as TClient
           ?? throw new InvalidOperationException(
               $"The message bus client for provider '{providerName}' does not support {Describe<TClient>()}. " +
               $"The client must implement {typeof(TClient).Name}.");

    public static string Describe<TClient>() => typeof(TClient) switch
    {
        var type when type == typeof(IMessageBusClient) => "queue-based messaging",
        var type when type == typeof(IEventCapableMessageBusClient) => "events",
        var type when type == typeof(IStreamCapableMessageBusClient) => "message streaming",
        var type when type == typeof(IPartitionedStreamCapableMessageBusClient) => "partitioned message streaming",
        var type => type.Name
    };

    public static string DescribeSupported(this ITransportClient client)
    {
        var supported = new List<string>(4);

        if (client is IMessageBusClient)
        {
            supported.Add(Describe<IMessageBusClient>());
        }

        if (client is IEventCapableMessageBusClient)
        {
            supported.Add(Describe<IEventCapableMessageBusClient>());
        }

        if (client is IStreamCapableMessageBusClient)
        {
            supported.Add(Describe<IStreamCapableMessageBusClient>());
        }

        if (client is IPartitionedStreamCapableMessageBusClient)
        {
            supported.Add(Describe<IPartitionedStreamCapableMessageBusClient>());
        }

        return supported.Count == 0 ? "no known capabilities" : string.Join(", ", supported);
    }

    public static string DescribeRegistered(IEnumerable<(string Name, ITransportClient Client)> providers)
        => $"Registered providers: {string.Join("; ", providers.Select(provider => $"{provider.Name} ({provider.Client.DescribeSupported()})"))}.";
}
