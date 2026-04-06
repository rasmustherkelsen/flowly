using System.Reflection;
using System.Runtime.CompilerServices;

namespace Flowly.MessageInfrastructure.Receivers;

public static class BatchMessageHandlerOptionsResolver
{
    private const int DefaultMaxMessagesBeforeProcessing = 100;
    private static readonly TimeSpan DefaultMaxWaitTime = TimeSpan.FromSeconds(30);

    public static ResolvedBatchMessageHandlerOptions Resolve<THandler>() where THandler : class
    {
        var handlerType = typeof(THandler);
        var options = new BatchMessageHandlerOptions();

        ApplyAttributes(handlerType, options);
        ApplyConfigure(handlerType, options);

        return new ResolvedBatchMessageHandlerOptions(
            options.MaxMessagesBeforeProcessing ?? DefaultMaxMessagesBeforeProcessing,
            options.MaxWaitTime ?? DefaultMaxWaitTime);
    }

    private static void ApplyAttributes(Type handlerType, BatchMessageHandlerOptions options)
    {
        var batchProcessingAttribute = handlerType.GetCustomAttribute<BatchProcessingAttribute>();
        if (batchProcessingAttribute != null)
        {
            options.MaxMessagesBeforeProcessing = batchProcessingAttribute.MaxMessagesBeforeProcessing;
            options.MaxWaitTime = TimeSpan.FromSeconds(batchProcessingAttribute.MaxWaitTimeInSeconds);
        }
    }

    private static void ApplyConfigure(Type handlerType, BatchMessageHandlerOptions options)
    {
        var configureMethod = handlerType.GetMethod(nameof(BatchMessageHandlerBase<object>.Configure), [typeof(BatchMessageHandlerOptions)]);

        if (configureMethod is null || configureMethod.DeclaringType == typeof(BatchMessageHandlerBase<>))
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
