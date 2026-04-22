using System.Diagnostics;
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
        var configureMethod = handlerType.GetMethod(nameof(BatchMessageHandler<object>.Configure), [typeof(BatchMessageHandlerOptions)]);

        if (configureMethod is null || configureMethod.DeclaringType == typeof(BatchMessageHandler<>))
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
                    "Flowly: {0} has a Configure(BatchMessageHandlerOptions) override but no parameterless constructor. " +
                    "Configure was invoked on an uninitialized instance — constructor-injected state will be null/default. " +
                    "Configure must not read fields set by the constructor.",
                    handlerType.FullName);

                return instance;
            }
            catch
            {
                Trace.TraceWarning(
                    "Flowly: {0} has a Configure(BatchMessageHandlerOptions) override but could not be instantiated. " +
                    "Configure will be skipped and queue options will fall back to attribute defaults.",
                    handlerType.FullName);

                return null;
            }
        }
    }
}
