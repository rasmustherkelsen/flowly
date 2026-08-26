using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Flowly.Transport;

namespace Flowly.AzureServiceBus;

internal class MessagingTopologyCreator(ServiceBusClient serviceBusClient, ServiceBusAdministrationClient adminClient) : IMessagingTopologyCreator, IEventTopologyCreator
{
    private static readonly string[] EmulatorHostPrefixes = ["localhost", "127.0.0.1", "::1", "[::1]", "host.docker.internal"];

    public async Task CreateEventTopology(IReadOnlyCollection<IEventDescription> eventDescriptions, CancellationToken cancellationToken)
    {
        if (IsEmulator()) throw new InvalidOperationException("Creating event topology is not supported when using the Azure Service Bus emulator.");

        await Task.WhenAll(eventDescriptions.Select(eventDescription => CreateEventTopologyEntry(eventDescription, cancellationToken)));
    }

    public async Task CreateTopology(IReadOnlyCollection<IQueueDescription> queueDescriptions, CancellationToken cancellationToken)
    {
        if (IsEmulator()) throw new InvalidOperationException("Creating messaging topology is not supported when using the Azure Service Bus emulator.");

        await Task.WhenAll(queueDescriptions.Select(queue => EnsureQueueExists(queue, cancellationToken)));
    }

    private async Task CreateEventTopologyEntry(IEventDescription eventDescription, CancellationToken cancellationToken)
    {
        await EnsureTopicExists(eventDescription, cancellationToken);

        if (eventDescription is IEventSubscriptionDescription subscriptionDescription)
            await EnsureSubscriptionExists(subscriptionDescription, cancellationToken);
    }

    private async Task EnsureQueueExists(IQueueDescription queue, CancellationToken cancellationToken)
    {
        await CreateIfNotExistsAsync(
            async () => (await adminClient.QueueExistsAsync(queue.Name, cancellationToken)).Value,
            () => adminClient.CreateQueueAsync(BuildQueueOptions(queue), cancellationToken));
    }

    private static CreateQueueOptions BuildQueueOptions(IQueueDescription queue)
    {
        if (queue is IReplyQueueDescription)
            return new CreateQueueOptions(queue.Name);

        return new CreateQueueOptions(queue.Name)
        {
            DefaultMessageTimeToLive = queue.DefaultMessageTimeToLive,
            DeadLetteringOnMessageExpiration = queue.DeadLetterOnMessageExpiration,
            LockDuration = queue.LockDuration,
            RequiresSession = queue.RequiresSession
        };
    }

    private async Task EnsureTopicExists(IEventDescription eventDescription, CancellationToken cancellationToken)
    {
        await CreateIfNotExistsAsync(
            async () => (await adminClient.TopicExistsAsync(eventDescription.TopicName, cancellationToken)).Value,
            () => adminClient.CreateTopicAsync(BuildTopicOptions(eventDescription), cancellationToken));
    }

    private static CreateTopicOptions BuildTopicOptions(IEventDescription eventDescription)
    {
        var topicOptions = new CreateTopicOptions(eventDescription.TopicName);

        if (eventDescription.DefaultMessageTimeToLive.HasValue)
            topicOptions.DefaultMessageTimeToLive = eventDescription.DefaultMessageTimeToLive.Value;

        return topicOptions;
    }

    private async Task EnsureSubscriptionExists(IEventSubscriptionDescription subscriptionDescription, CancellationToken cancellationToken)
    {
        var subscriptionExists = await adminClient.SubscriptionExistsAsync(
            subscriptionDescription.TopicName,
            subscriptionDescription.SubscriptionName,
            cancellationToken);

        if (subscriptionExists.Value)
        {
            await EnsureSubscriptionFilterRule(
                subscriptionDescription.TopicName,
                subscriptionDescription.SubscriptionName,
                BuildTargetedFilter(subscriptionDescription.SubscriptionName),
                cancellationToken);

            return;
        }

        await CreateIgnoringAlreadyExists(() => adminClient.CreateSubscriptionAsync(
            BuildSubscriptionOptions(subscriptionDescription),
            new CreateRuleOptions("flowly-targeted", BuildTargetedFilter(subscriptionDescription.SubscriptionName)),
            cancellationToken));
    }

    private static CreateSubscriptionOptions BuildSubscriptionOptions(IEventSubscriptionDescription subscriptionDescription)
    {
        var subscriptionOptions = new CreateSubscriptionOptions(
            subscriptionDescription.TopicName,
            subscriptionDescription.SubscriptionName)
        {
            DeadLetteringOnMessageExpiration = subscriptionDescription.DeadLetterOnMessageExpiration ?? true,
            LockDuration = TimeSpan.FromMinutes(5),
            MaxDeliveryCount = 10
        };

        if (subscriptionDescription.DefaultMessageTimeToLive.HasValue)
            subscriptionOptions.DefaultMessageTimeToLive = subscriptionDescription.DefaultMessageTimeToLive.Value;

        return subscriptionOptions;
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

        await CreateIgnoringAlreadyExists(() => adminClient.CreateRuleAsync(
            topicName,
            subscriptionName,
            new CreateRuleOptions("flowly-targeted", targetedFilter),
            cancellationToken));
    }

    private static async Task CreateIfNotExistsAsync(Func<Task<bool>> existsCheck, Func<Task> create)
    {
        if (await existsCheck())
            return;

        await CreateIgnoringAlreadyExists(create);
    }

    private static async Task CreateIgnoringAlreadyExists(Func<Task> create)
    {
        try
        {
            await create();
        }
        catch (ServiceBusException e) when (e.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists)
        {
        }
    }

    private static SqlRuleFilter BuildTargetedFilter(string subscriptionName)
    {
        return new SqlRuleFilter($"(NOT EXISTS([{FlowlyMessageProperties.TargetSubscription}])) OR [{FlowlyMessageProperties.TargetSubscription}] = '{subscriptionName}'");
    }

    private bool IsEmulator() => IsEmulatorHost(serviceBusClient.FullyQualifiedNamespace);

    internal static bool IsEmulatorHost(string fullyQualifiedNamespace)
    {
        return EmulatorHostPrefixes.Any(prefix => fullyQualifiedNamespace.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }
}
