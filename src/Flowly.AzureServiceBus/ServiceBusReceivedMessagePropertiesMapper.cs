using System.Buffers.Binary;
using Azure.Messaging.ServiceBus;
using Flowly.Transport;

namespace Flowly.AzureServiceBus;

internal static class ServiceBusReceivedMessagePropertiesMapper
{
    public static MessageProperties Map(ServiceBusReceivedMessage message) => new(
        message.MessageId,
        message.CorrelationId,
        SessionId: message.SessionId,
        RetryCount: GetRetryCount(message.ApplicationProperties),
        Traceparent: GetStringProperty(message.ApplicationProperties, "traceparent"),
        Tracestate: GetStringProperty(message.ApplicationProperties, "tracestate"),
        ReplyTo: string.IsNullOrEmpty(message.ReplyTo) ? null : message.ReplyTo,
        DeliveryCount: message.DeliveryCount);

    private static int GetRetryCount(IReadOnlyDictionary<string, object> applicationProperties)
    {
        if (!applicationProperties.TryGetValue(FlowlyMessageProperties.RetryCount, out var value)) return 0;

        return value switch
        {
            int i => i,
            long l => (int)l,
            byte[] { Length: 4 } b => BinaryPrimitives.ReadInt32LittleEndian(b),
            _ => 0
        };
    }

    private static string? GetStringProperty(IReadOnlyDictionary<string, object> applicationProperties, string key)
        => applicationProperties.TryGetValue(key, out var value) ? value as string : null;
}
