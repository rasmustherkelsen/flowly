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

    public static ResolvedHandlerQueueOptions Resolve<THandler>() where THandler : class
    {
        var handlerType = typeof(THandler);
        var options = new HandlerQueueOptions();

        ApplyAttributes(handlerType, options);
        ApplyConfigure(handlerType, options);

        var resolvedQueueName = string.IsNullOrWhiteSpace(options.QueueName)
            ? DeriveQueueName(handlerType)
            : options.QueueName!;

        return new ResolvedHandlerQueueOptions(
            resolvedQueueName,
            options.DefaultMessageTimeToLive ?? DefaultMessageTimeToLive,
            options.DeadLetterOnMessageExpiration ?? DefaultDeadLetterOnMessageExpiration,
            options.LockDuration ?? DefaultLockDuration);
    }

    private static void ApplyAttributes(Type handlerType, HandlerQueueOptions options)
    {
        var queueNameAttribute = handlerType.GetCustomAttribute<QueueNameAttribute>();
        if (queueNameAttribute != null)
        {
            options.QueueName = queueNameAttribute.QueueName;
        }

        var ttlAttribute = handlerType.GetCustomAttribute<DefaultMessageTimeToLiveAttribute>();
        if (ttlAttribute != null)
        {
            options.DefaultMessageTimeToLive = ttlAttribute.GetValue();
        }

        var deadLetterAttribute = handlerType.GetCustomAttribute<DeadLetterOnMessageExpirationAttribute>();
        if (deadLetterAttribute != null)
        {
            options.DeadLetterOnMessageExpiration = deadLetterAttribute.Enabled;
        }

        var lockDurationAttribute = handlerType.GetCustomAttribute<LockDurationAttribute>();
        if (lockDurationAttribute != null)
        {
            options.LockDuration = lockDurationAttribute.GetValue();
        }
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

    private static string DeriveQueueName(Type handlerType)
    {
        var name = handlerType.Name;

        if (name.EndsWith("MessageHandler", StringComparison.OrdinalIgnoreCase))
        {
            return name[..^"MessageHandler".Length];
        }

        if (name.EndsWith("Handler", StringComparison.OrdinalIgnoreCase))
        {
            return name[..^"Handler".Length];
        }

        return name;
    }
}
