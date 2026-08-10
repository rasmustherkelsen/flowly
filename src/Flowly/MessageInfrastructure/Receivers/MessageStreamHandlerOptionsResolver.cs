using System.Reflection;

namespace Flowly.MessageInfrastructure.Receivers;

internal static class MessageStreamHandlerOptionsResolver
{
    private const int DefaultMaxRetries = 0;
    private const int DefaultRetryDelaySeconds = 0;
    private const int DefaultMaxMessagesBeforeProcessing = 100;
    private static readonly TimeSpan DefaultMaxWaitTime = TimeSpan.FromSeconds(30);

    public static ResolvedMessageStreamHandlerOptions Resolve<THandler, TMessage>(ITopologyNameResolver topologyNameResolver) where THandler : class
    {
        var handlerType = typeof(THandler);
        var options = new MessageStreamHandlerOptions
        {
            QueueName = topologyNameResolver.ResolveQueueName<TMessage>()
        };

        ApplyContractAttributes(typeof(TMessage), options);
        ApplyHandlerAttributes(handlerType, options);
        ApplyConfigure(handlerType, options);

        if (options.StartPosition is null)
            throw new InvalidOperationException(
                $"{handlerType.Name} does not set a start position. Stream handlers must explicitly choose where consumption begins — " +
                $"override Configure(MessageStreamHandlerOptions) and set options.StartPosition to StartPosition.First(), " +
                $"StartPosition.Last(), StartPosition.Offset(n), or StartPosition.Timestamp(dt).");

        var retryPolicyAttribute = handlerType.GetCustomAttribute<RetryPolicyAttribute>();

        return new ResolvedMessageStreamHandlerOptions(
            options.QueueName!,
            options.StartPosition.Value,
            options.MaxMessagesBeforeProcessing ?? DefaultMaxMessagesBeforeProcessing,
            options.MaxWaitTime ?? DefaultMaxWaitTime,
            retryPolicyAttribute?.MaxRetries ?? DefaultMaxRetries,
            retryPolicyAttribute?.DelaySeconds ?? DefaultRetryDelaySeconds,
            options.MaxAgeSeconds,
            options.MaxLengthBytes);
    }

    private static void ApplyContractAttributes(Type messageType, MessageStreamHandlerOptions options)
    {
        var retentionAttribute = messageType.GetCustomAttribute<StreamRetentionAttribute>();
        if (retentionAttribute == null) return;

        options.MaxAgeSeconds = retentionAttribute.MaxAgeSeconds;
        options.MaxLengthBytes = retentionAttribute.MaxLengthBytes;
    }

    private static void ApplyHandlerAttributes(Type handlerType, MessageStreamHandlerOptions options)
    {
        var batchProcessingAttribute = handlerType.GetCustomAttribute<BatchProcessingAttribute>();
        if (batchProcessingAttribute != null)
        {
            options.MaxMessagesBeforeProcessing = batchProcessingAttribute.MaxMessagesBeforeProcessing;
            options.MaxWaitTime = TimeSpan.FromSeconds(batchProcessingAttribute.MaxWaitTimeInSeconds);
        }
    }

    private static void ApplyConfigure(Type handlerType, MessageStreamHandlerOptions options)
    {
        var configureMethod = handlerType.GetMethod(nameof(MessageStreamHandler<object>.Configure), [typeof(MessageStreamHandlerOptions)]);
        if (configureMethod is null || configureMethod.DeclaringType == typeof(MessageStreamHandler<>)) return;

        if (HandlerConfigureInvoker.CreateInstanceForConfigure(handlerType) is { } instance)
            configureMethod.Invoke(instance, [options]);
    }
}

internal sealed record ResolvedMessageStreamHandlerOptions(
    string QueueName,
    StartPosition StartPosition,
    int MaxMessagesBeforeProcessing,
    TimeSpan MaxWaitTime,
    int MaxRetries,
    int RetryDelaySeconds,
    int? MaxAgeSeconds,
    long? MaxLengthBytes);
