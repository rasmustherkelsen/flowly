using System.Text.Json;
using Flowly.DeadLetters.BackgroundServices;
using Flowly.DeadLetters.DatabaseModel;
using Flowly.DeadLetters.Repositories;
using Flowly.MessageInfrastructure.Registration;
using Flowly.Transport;

namespace Flowly.DeadLetters.Services;

internal class DeadLetterService(
    IDeadLetterRepository repository,
    IMessageBusClientRegistry clientRegistry,
    IEnumerable<DeadLetterIngestionSettings> ingestionSettings,
    IEnumerable<EventSubscriptionDeadLetterIngestionSettings> eventSubscriptionIngestionSettings) : IDeadLetterService
{
    public async Task<IReadOnlyCollection<DeadLetter>> GetDeadLetters(CancellationToken cancellationToken = default)
    {
        return await repository.GetAll(cancellationToken);
    }

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

        applicationProperties.Remove(FlowlyMessageProperties.RetryCount);

        if (deadLetter.SubscriptionName is not null)
            applicationProperties[FlowlyMessageProperties.TargetSubscription] = deadLetter.SubscriptionName;

        var providerName = ResolveProviderName(deadLetter.QueueName);
        var client = clientRegistry.GetClient(providerName);
        var sender = await ResolveSender(client, deadLetter, cancellationToken);
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

    private static Task<IMessageBusSender> ResolveSender(IMessageBusClient client, DeadLetter deadLetter, CancellationToken cancellationToken)
    {
        if (deadLetter.SubscriptionName is not null)
        {
            if (client is not IEventCapableMessageBusClient eventCapableClient)
                throw new InvalidOperationException(
                    "The message bus client does not support events and cannot requeue event subscription dead letters.");

            return eventCapableClient.CreateEventRetrySender(deadLetter.QueueName, deadLetter.SubscriptionName);
        }

        return client.CreateMessageBusSender(deadLetter.QueueName);
    }

    private string ResolveProviderName(string queueOrTopicName)
    {
        var queueSettings = ingestionSettings
            .FirstOrDefault(s => string.Equals(s.QueueName, queueOrTopicName, StringComparison.OrdinalIgnoreCase));

        if (queueSettings != null)
            return queueSettings.ProviderName;

        var subscriptionSettings = eventSubscriptionIngestionSettings
            .FirstOrDefault(s => string.Equals(s.TopicOrExchangeName, queueOrTopicName, StringComparison.OrdinalIgnoreCase));

        return subscriptionSettings?.ProviderName ?? clientRegistry.PrimaryProviderName;
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