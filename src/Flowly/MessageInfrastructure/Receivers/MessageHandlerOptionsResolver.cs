using System.Reflection;

namespace Flowly.MessageInfrastructure.Receivers;

internal static class MessageHandlerOptionsResolver
{
    private const bool DefaultDeadLetterOnMessageExpiration = true;
    private const int DefaultMaxRetries = 0;
    private const int DefaultRetryDelaySeconds = 0;
    private const int DefaultMaxConcurrentCalls = 1;
    private const int DefaultMaxMessagesBeforeProcessing = 100;
    private static readonly TimeSpan DefaultLockDuration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan DefaultMessageTimeToLive = TimeSpan.FromDays(1);
    private static readonly TimeSpan DefaultMaxWaitTime = TimeSpan.FromSeconds(30);

    public static ResolvedMessageHandlerOptions Resolve<THandler, TMessage>(ITopologyNameResolver topologyNameResolver) where THandler : class
    {
        var handlerType = typeof(THandler);
        var options = new BatchMessageHandlerOptions
        {
            QueueName = topologyNameResolver.ResolveQueueName<TMessage>()
        };

        ApplyAttributes(handlerType, options);
        ApplyConfigure(handlerType, options);

        return new ResolvedMessageHandlerOptions(
            options.QueueName,
            options.DefaultMessageTimeToLive ?? DefaultMessageTimeToLive,
            options.DeadLetterOnMessageExpiration ?? DefaultDeadLetterOnMessageExpiration,
            options.LockDuration ?? DefaultLockDuration,
            options.MaxRetries ?? DefaultMaxRetries,
            options.RetryDelaySeconds ?? DefaultRetryDelaySeconds,
            options.MaxConcurrentCalls ?? DefaultMaxConcurrentCalls,
            options.MaxMessagesBeforeProcessing ?? DefaultMaxMessagesBeforeProcessing,
            options.MaxWaitTime ?? DefaultMaxWaitTime);
    }

    private static void ApplyAttributes(Type handlerType, BatchMessageHandlerOptions options)
    {
        var ttlAttribute = handlerType.GetCustomAttribute<DefaultMessageTimeToLiveAttribute>();
        if (ttlAttribute != null)
            options.DefaultMessageTimeToLive = ttlAttribute.TimeToLive;

        var deadLetterAttribute = handlerType.GetCustomAttribute<DeadLetterOnMessageExpirationAttribute>();
        if (deadLetterAttribute != null)
            options.DeadLetterOnMessageExpiration = deadLetterAttribute.Enabled;

        var lockDurationAttribute = handlerType.GetCustomAttribute<LockDurationAttribute>();
        if (lockDurationAttribute != null)
            options.LockDuration = lockDurationAttribute.LockDuration;

        var retryPolicyAttribute = handlerType.GetCustomAttribute<RetryPolicyAttribute>();
        if (retryPolicyAttribute != null)
        {
            options.MaxRetries = retryPolicyAttribute.MaxRetries;
            options.RetryDelaySeconds = retryPolicyAttribute.DelaySeconds;
        }

        var maxConcurrentCallsAttribute = handlerType.GetCustomAttribute<MaxConcurrentCallsAttribute>();
        if (maxConcurrentCallsAttribute != null)
            options.MaxConcurrentCalls = maxConcurrentCallsAttribute.MaxConcurrentCalls;

        var batchProcessingAttribute = handlerType.GetCustomAttribute<BatchProcessingAttribute>();
        if (batchProcessingAttribute != null)
        {
            options.MaxMessagesBeforeProcessing = batchProcessingAttribute.MaxMessagesBeforeProcessing;
            options.MaxWaitTime = TimeSpan.FromSeconds(batchProcessingAttribute.MaxWaitTimeInSeconds);
        }
    }

    private static void ApplyConfigure(Type handlerType, BatchMessageHandlerOptions options)
    {
        var batchConfigureMethod = handlerType.GetMethod(nameof(BatchMessageHandler<>.Configure), [typeof(BatchMessageHandlerOptions)]);
        if (batchConfigureMethod is not null && batchConfigureMethod.DeclaringType != typeof(BatchMessageHandler<>))
        {
            if (HandlerConfigureInvoker.CreateInstanceForConfigure(handlerType) is { } handlerInstance)
                batchConfigureMethod.Invoke(handlerInstance, [options]);

            return;
        }

        var configureMethod = handlerType.GetMethod(nameof(MessageHandler<>.Configure), [typeof(HandlerQueueOptions)]);
        if (configureMethod is null || configureMethod.DeclaringType == typeof(MessageHandler<>)) return;

        if (HandlerConfigureInvoker.CreateInstanceForConfigure(handlerType) is { } instance)
            configureMethod.Invoke(instance, [options]);
    }
}