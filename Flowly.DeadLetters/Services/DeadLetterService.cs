using System.Text.Json;
using Flowly.DeadLetters.DatabaseModel;
using Flowly.DeadLetters.Repositories;
using Flowly.MessagingAbstractions;

namespace Flowly.DeadLetters.Services;

internal class DeadLetterService(IDeadLetterRepository repository, IMessageBusClient messageBusClient) : IDeadLetterService
{
    public async Task Requeue(string messageId, string? requeuedBy = null, CancellationToken cancellationToken = default)
    {
        var deadLetter = await repository.Get(messageId, cancellationToken)
            ?? throw new KeyNotFoundException($"Dead letter with ID '{messageId}' was not found.");

        if (deadLetter.Status != DeadLetterStatus.Pending)
            throw new InvalidOperationException(
                $"Dead letter '{messageId}' has status '{deadLetter.Status}' and cannot be requeued.");

        var rawProperties = JsonSerializer.Deserialize<Dictionary<string, object>>(deadLetter.MessageProperties) ?? [];
        var applicationProperties = rawProperties.ToDictionary(
            kvp => kvp.Key,
            kvp => ConvertJsonElement(kvp.Value));

        var sender = messageBusClient.CreateMessageBusSender(deadLetter.QueueName);
        await sender.SendRawMessage(deadLetter.MessageBody, applicationProperties, cancellationToken);

        await repository.MarkAsRequeued(messageId, requeuedBy, cancellationToken);
    }

    public async Task Discard(string messageId, CancellationToken cancellationToken = default)
    {
        var deadLetter = await repository.Get(messageId, cancellationToken)
            ?? throw new KeyNotFoundException($"Dead letter with ID '{messageId}' was not found.");

        if (deadLetter.Status == DeadLetterStatus.Requeued)
            throw new InvalidOperationException(
                $"Dead letter '{messageId}' has already been requeued and cannot be discarded.");

        await repository.Delete(messageId, cancellationToken);
    }

    private static object ConvertJsonElement(object value)
    {
        if (value is not JsonElement element)
            return value;

        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Number when element.TryGetInt32(out var i) => i,
            JsonValueKind.Number when element.TryGetInt64(out var l) => l,
            JsonValueKind.Number => element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => element.ToString()
        };
    }
}
