using System.Diagnostics;
using System.Text.Json;
using Flowly.DeadLetters.BackgroundServices;
using Flowly.DeadLetters.Repositories;
using Flowly.DeadLetters.Telemetry;
using Flowly.MessageInfrastructure.Registration;
using Flowly.Transport;

namespace Flowly.DeadLetters.Services;

internal class DeadLetterService(
    IDeadLetterRepository repository,
    IMessageBusClientRegistry clientRegistry,
    IEnumerable<DeadLetterIngestionSettings> ingestionSettings,
    IEnumerable<EventSubscriptionDeadLetterIngestionSettings> eventSubscriptionIngestionSettings,
    IDeadLetterOperationInstrumentation instrumentation) : IDeadLetterService
{
    public async Task<IReadOnlyCollection<IDeadLetter>> GetDeadLetters(CancellationToken cancellationToken = default)
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
        var applicationProperties = DeadLetterPropertiesConverter.ConvertProperties(rawProperties);

        applicationProperties.Remove(FlowlyMessageProperties.RetryCount);

        if (deadLetter.SubscriptionName is not null)
            applicationProperties[FlowlyMessageProperties.TargetSubscription] = deadLetter.SubscriptionName;

        var originalContext = DeadLetterPropertiesConverter.ParseActivityContext(applicationProperties);
        using var activity = instrumentation.StartRequeue(deadLetter.QueueName, messageId, originalContext);

        try
        {
            var providerName = ResolveProviderName(deadLetter.QueueName);
            var client = clientRegistry.GetClient(providerName);
            var sender = await ResolveSender(client, deadLetter, cancellationToken);
            await sender.SendRawMessage(deadLetter.MessageBody, applicationProperties, cancellationToken);

            await repository.MarkAsRequeued(messageId, requeuedBy, cancellationToken);

            instrumentation.RecordRequeued(deadLetter.QueueName);
        }
        catch
        {
            activity?.SetStatus(ActivityStatusCode.Error);
            throw;
        }
    }

    public async Task Discard(string messageId, CancellationToken cancellationToken = default)
    {
        var deadLetter = await repository.Get(messageId, cancellationToken)
                         ?? throw new KeyNotFoundException($"Dead letter with ID '{messageId}' was not found.");

        if (deadLetter.Status == DeadLetterStatus.Requeued)
            throw new InvalidOperationException(
                $"Dead letter '{messageId}' has already been requeued and cannot be discarded.");

        var originalContext = DeadLetterPropertiesConverter.ParseActivityContext(deadLetter.MessageProperties);
        using var activity = instrumentation.StartDiscard(deadLetter.QueueName, messageId, originalContext);

        try
        {
            await repository.Delete(messageId, cancellationToken);

            instrumentation.RecordDiscarded(deadLetter.QueueName, DeadLetterDiscardReason.UserInitiated);
        }
        catch
        {
            activity?.SetStatus(ActivityStatusCode.Error);
            throw;
        }
    }

    private static Task<IMessageBusSender> ResolveSender(IMessageBusClient client, IDeadLetter deadLetter, CancellationToken cancellationToken)
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
            .FirstOrDefault(s => string.Equals(s.TopicName, queueOrTopicName, StringComparison.OrdinalIgnoreCase));

        return subscriptionSettings?.ProviderName ?? clientRegistry.PrimaryProviderName;
    }
}
