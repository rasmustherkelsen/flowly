using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Flowly.MessageInfrastructure.Receivers;

internal static class HandlerQueueOptionsResolver
{
    private static readonly TimeSpan DefaultLockDuration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan DefaultMessageTimeToLive = TimeSpan.FromDays(1);
    private const bool DefaultDeadLetterOnMessageExpiration = true;

    public static ResolvedHandlerQueueOptions Resolve<THandler, TMessage>() where THandler : class
    {
        var handlerType = typeof(THandler);
        var options = new HandlerQueueOptions();

        options.QueueName = MessageQueueNameResolver.Resolve<TMessage>();

        ApplyHandlerAttributes(handlerType, options);
        ApplyConfigure(handlerType, options);

        var resolvedQueueName = options.QueueName!;

        return new ResolvedHandlerQueueOptions(
            resolvedQueueName,
            options.DefaultMessageTimeToLive ?? DefaultMessageTimeToLive,
            options.DeadLetterOnMessageExpiration ?? DefaultDeadLetterOnMessageExpiration,
            options.LockDuration ?? DefaultLockDuration,
            options.MaxRetries ?? 0,
            options.RetryDelaySeconds ?? 0,
            options.MaxConcurrentCalls ?? 1);
    }

    private static void ApplyHandlerAttributes(Type handlerType, HandlerQueueOptions options)
    {
        var ttlAttribute = handlerType.GetCustomAttribute<DefaultMessageTimeToLiveAttribute>();
        if (ttlAttribute != null)
            options.DefaultMessageTimeToLive = ttlAttribute.GetValue();

        var deadLetterAttribute = handlerType.GetCustomAttribute<DeadLetterOnMessageExpirationAttribute>();
        if (deadLetterAttribute != null)
            options.DeadLetterOnMessageExpiration = deadLetterAttribute.Enabled;

        var lockDurationAttribute = handlerType.GetCustomAttribute<LockDurationAttribute>();
        if (lockDurationAttribute != null)
            options.LockDuration = lockDurationAttribute.GetValue();

        var retryPolicyAttribute = handlerType.GetCustomAttribute<RetryPolicyAttribute>();
        if (retryPolicyAttribute != null)
        {
            options.MaxRetries = retryPolicyAttribute.MaxRetries;
            options.RetryDelaySeconds = retryPolicyAttribute.DelaySeconds;
        }

        var maxConcurrentCallsAttribute = handlerType.GetCustomAttribute<MaxConcurrentCallsAttribute>();
        if (maxConcurrentCallsAttribute != null)
            options.MaxConcurrentCalls = maxConcurrentCallsAttribute.MaxConcurrentCalls;
    }

    private static void ApplyConfigure(Type handlerType, HandlerQueueOptions options)
    {
        var configureMethod = handlerType.GetMethod(nameof(MessageHandler<object>.Configure), [typeof(HandlerQueueOptions)]);

        if (configureMethod is null || configureMethod.DeclaringType == typeof(MessageHandler<>))
        {
            return;
        }

        if (CreateHandlerInstanceForConfigure(handlerType) is not { } handlerInstance)
        {
            return;
        }

        configureMethod.Invoke(handlerInstance, [options]);
    }

    private static object? CreateHandlerInstanceForConfigure(Type handlerType)
    {
        try
        {
            return Activator.CreateInstance(handlerType);
        }
        catch
        {
            try
            {
                var instance = RuntimeHelpers.GetUninitializedObject(handlerType);
                Trace.TraceWarning(
                    "Flowly: {0} has a Configure(HandlerQueueOptions) override but no parameterless constructor. " +
                    "Configure was invoked on an uninitialized instance — constructor-injected state will be null/default. " +
                    "Configure must not read fields set by the constructor.",
                    handlerType.FullName);

                return instance;
            }
            catch
            {
                Trace.TraceWarning(
                    "Flowly: {0} has a Configure(HandlerQueueOptions) override but could not be instantiated. " +
                    "Configure will be skipped and queue options will fall back to attribute defaults.",
                    handlerType.FullName);

                return null;
            }
        }
    }

}
