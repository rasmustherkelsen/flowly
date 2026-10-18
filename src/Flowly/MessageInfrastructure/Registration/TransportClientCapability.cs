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

    private static string Describe<TClient>() => typeof(TClient) switch
    {
        var type when type == typeof(IMessageBusClient) => "queue-based messaging",
        var type when type == typeof(IEventCapableMessageBusClient) => "events",
        var type when type == typeof(IStreamCapableMessageBusClient) => "message streaming",
        var type when type == typeof(IPartitionedStreamCapableMessageBusClient) => "partitioned message streaming",
        var type => type.Name
    };
}
