using System.Text.Json;
using Flowly.Transport;

namespace Flowly.InMemory;

internal class InMemoryUntypedReceivedMessage(InMemoryEnvelope envelope) : IReceivedMessage
{
    public TBody GetBody<TBody>()
        => JsonSerializer.Deserialize<TBody>(envelope.RawBody)
           ?? throw new InvalidOperationException($"Deserialized message body is null for type {typeof(TBody).FullName}.");

    public MessageProperties Properties => InMemoryReceivedMessage<object>.BuildProperties(envelope);
}