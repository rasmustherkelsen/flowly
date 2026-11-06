using System.Text.Json;
using Azure.Messaging.EventHubs;

namespace Flowly.AzureEventHubs.Tests.Fakes;

internal static class TestEvents
{
    public static EventData Create(
        long sequenceNumber,
        string body = "body",
        string? messageId = null,
        string? correlationId = null,
        IDictionary<string, object>? properties = null)
    {
        var eventData = EventHubsModelFactory.EventData(
            new BinaryData(JsonSerializer.SerializeToUtf8Bytes(body)),
            properties: properties,
            sequenceNumber: sequenceNumber,
            offsetString: "offset");

        if (messageId is not null)
            eventData.MessageId = messageId;

        if (correlationId is not null)
            eventData.CorrelationId = correlationId;

        return eventData;
    }

    public static EventData Raw(long sequenceNumber, string json)
        => EventHubsModelFactory.EventData(new BinaryData(json), sequenceNumber: sequenceNumber, offsetString: "offset");
}
