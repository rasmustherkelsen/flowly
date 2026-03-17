using System.Reflection;
using System.Runtime.CompilerServices;

namespace Flowly.MessageInfrastructure.Receivers;

public sealed record ResolvedHandlerQueueOptions(
    string QueueName,
    TimeSpan DefaultMessageTimeToLive,
    bool DeadLetterOnMessageExpiration,
    TimeSpan LockDuration);

public static class HandlerQueueOptionsResolver
{
    public static readonly TimeSpan DefaultLockDuration = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan DefaultMessageTimeToLive = TimeSpan.FromDays(1);
    public const bool DefaultDeadLetterOnMessageExpiration = true;

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
            options.LockDuration ?? DefaultLockDuration);
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
    }

    private static void ApplyConfigure(Type handlerType, HandlerQueueOptions options)
    {
        var configureMethod = handlerType.GetMethod(nameof(MessageHandlerBase<object>.Configure), [typeof(HandlerQueueOptions)]);

        if (configureMethod is null || configureMethod.DeclaringType == typeof(MessageHandlerBase<>))
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
                return RuntimeHelpers.GetUninitializedObject(handlerType);
            }
            catch
            {
                return null;
            }
        }
    }

}
