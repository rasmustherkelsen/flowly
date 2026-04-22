using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Flowly.Transport;

namespace Flowly.AzureServiceBus;

internal class MessagingTopologyCreator(ServiceBusClient serviceBusClient, ServiceBusAdministrationClient adminClient) : IMessagingTopologyCreator, IEventTopologyCreator
{
    public async Task CreateEventTopology(IReadOnlyCollection<IEventDescription> eventDescriptions, CancellationToken cancellationToken)
    {
        if (IsEmulator()) throw new InvalidOperationException("Creating event topology is not supported when using the Azure Service Bus emulator.");

        foreach (var eventDescription in eventDescriptions)
        {
            await EnsureTopicExists(eventDescription, cancellationToken);
            await EnsureSubscriptionExists(eventDescription, cancellationToken);
        }
    }

    public async Task CreateTopology(IReadOnlyCollection<IQueueDescription> queueDescriptions, CancellationToken cancellationToken)
    {
        if (IsEmulator()) throw new InvalidOperationException("Creating messaging topology is not supported when using the Azure Service Bus emulator.");

        foreach (var queue in queueDescriptions)
        {
            var exists = await adminClient.QueueExistsAsync(queue.Name, cancellationToken);
            if (!exists.Value)
                await adminClient.CreateQueueAsync(new CreateQueueOptions(queue.Name)
                {
                    DefaultMessageTimeToLive = queue.DefaultMessageTimeToLive,
                    DeadLetteringOnMessageExpiration = queue.DeadLetterOnMessageExpiration,
                    LockDuration = queue.LockDuration,
                    RequiresSession = queue.RequiresSession
                }, cancellationToken);
        }
    }

    private async Task EnsureTopicExists(IEventDescription eventDescription, CancellationToken cancellationToken)
    {
        var topicExists = await adminClient.TopicExistsAsync(eventDescription.TopicName, cancellationToken);
        if (!topicExists.Value)
        {
            var topicOptions = new CreateTopicOptions(eventDescription.TopicName);

            if (eventDescription.DefaultMessageTimeToLive.HasValue)
                topicOptions.DefaultMessageTimeToLive = eventDescription.DefaultMessageTimeToLive.Value;

            await adminClient.CreateTopicAsync(topicOptions, cancellationToken);
        }
    }

    private async Task EnsureSubscriptionExists(IEventDescription eventDescription, CancellationToken cancellationToken)
    {
        var subscriptionExists = await adminClient.SubscriptionExistsAsync(
            eventDescription.TopicName,
            eventDescription.SubscriptionName,
            cancellationToken);

        var targetedFilter = BuildTargetedFilter(eventDescription.SubscriptionName);

        if (!subscriptionExists.Value)
        {
            var subscriptionOptions = new CreateSubscriptionOptions(
                eventDescription.TopicName,
                eventDescription.SubscriptionName)
            {
                DeadLetteringOnMessageExpiration = eventDescription.DeadLetterOnMessageExpiration ?? true,
                LockDuration = TimeSpan.FromMinutes(5),
                MaxDeliveryCount = 10
            };

            if (eventDescription.DefaultMessageTimeToLive.HasValue)
                subscriptionOptions.DefaultMessageTimeToLive = eventDescription.DefaultMessageTimeToLive.Value;

            var ruleOptions = new CreateRuleOptions("flowly-targeted", targetedFilter);
            await adminClient.CreateSubscriptionAsync(subscriptionOptions, ruleOptions, cancellationToken);
        }
        else
        {
            await EnsureSubscriptionFilterRule(
                eventDescription.TopicName,
                eventDescription.SubscriptionName,
                targetedFilter,
                cancellationToken);
        }
    }

    private async Task EnsureSubscriptionFilterRule(
        string topicName,
        string subscriptionName,
        SqlRuleFilter targetedFilter,
        CancellationToken cancellationToken)
    {
        try
        {
            await adminClient.DeleteRuleAsync(topicName, subscriptionName, "$Default", cancellationToken);
        }
        catch (ServiceBusException e) when (e.Reason == ServiceBusFailureReason.MessagingEntityNotFound)
        {
        }

        try
        {
            await adminClient.CreateRuleAsync(
                topicName,
                subscriptionName,
                new CreateRuleOptions("flowly-targeted", targetedFilter),
                cancellationToken);
        }
        catch (ServiceBusException e) when (e.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists)
        {
        }
    }

    private static SqlRuleFilter BuildTargetedFilter(string subscriptionName)
    {
        return new SqlRuleFilter($"(NOT EXISTS([{FlowlyMessageProperties.TargetSubscription}])) OR [{FlowlyMessageProperties.TargetSubscription}] = '{subscriptionName}'");
    }

    private bool IsEmulator()
    {
        var fullyQualifiedNamespace = serviceBusClient.FullyQualifiedNamespace;

        return fullyQualifiedNamespace.StartsWith("localhost", StringComparison.OrdinalIgnoreCase) ||
               fullyQualifiedNamespace.StartsWith("127.0.0.1", StringComparison.OrdinalIgnoreCase);
    }
}