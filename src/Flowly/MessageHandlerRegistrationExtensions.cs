using Flowly.MessageInfrastructure.BackgroundServices;
using Flowly.MessageInfrastructure.Model;
using Flowly.MessageInfrastructure.Receivers;
using Flowly.MessageInfrastructure.Registration;
using Flowly.Registration;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly;

public static class MessageHandlerRegistrationExtensions
{
    /// <summary>
    /// Registers a message handler for the specified message type.
    /// </summary>
    /// <param name="flowlyBuilder></param>
    /// <typeparam name="TMessage"></typeparam>
    /// <typeparam name="THandler"></typeparam>
    /// <returns></returns>
    public static IMessageHandlerBuilder<TMessage> AddMessageHandler<TMessage, THandler>(this IFlowlyBuilder flowlyBuilder)
        where THandler : MessageHandler<TMessage>
        where TMessage : class
    {
        var providerName = ProviderNameResolver.Resolve(flowlyBuilder.Services, typeof(TMessage));
        var resolvedQueueOptions = HandlerQueueOptionsResolver.Resolve<THandler, TMessage>();
        var resolvedQueueName = resolvedQueueOptions.QueueName;

        flowlyBuilder.AddQueueRegistration(new DeferredQueueRegistration(
            resolvedQueueName,
            false,
            resolvedQueueOptions.DefaultMessageTimeToLive,
            resolvedQueueOptions.DeadLetterOnMessageExpiration,
            resolvedQueueOptions.LockDuration),
            providerName);

        var handlerSettings = new HandlerSettings<TMessage>(resolvedQueueName, providerName, typeof(THandler).Name, false, resolvedQueueOptions.MaxConcurrentCalls, resolvedQueueOptions.MaxRetries, resolvedQueueOptions.RetryDelaySeconds);

        flowlyBuilder.Services
            .AddScoped<THandler>()
            .AddScoped<MessageHandler<TMessage>, THandler>()
            .AddSingleton(handlerSettings)
            .AddMessageProcessingPipeline(handlerSettings, _ => new StandardMessageHandlingStrategy<TMessage>());

        return new MessageHandlerBuilder<TMessage>(flowlyBuilder, resolvedQueueName, providerName);
    }

    public static IFlowlyBuilder AddBatchMessageHandler<TMessage, THandler>(this IFlowlyBuilder flowlyBuilder)
        where THandler : BatchMessageHandler<TMessage>
        where TMessage : class
    {
        var providerName = ProviderNameResolver.Resolve(flowlyBuilder.Services, typeof(TMessage));
        var resolvedQueueOptions = HandlerQueueOptionsResolver.Resolve<THandler, TMessage>();
        var resolvedBatchOptions = BatchMessageHandlerOptionsResolver.Resolve<THandler>();
        var resolvedQueueName = resolvedQueueOptions.QueueName;

        flowlyBuilder.AddQueueRegistration(new DeferredQueueRegistration(
            resolvedQueueName,
            false,
            resolvedQueueOptions.DefaultMessageTimeToLive,
            resolvedQueueOptions.DeadLetterOnMessageExpiration,
            resolvedQueueOptions.LockDuration),
            providerName);

        flowlyBuilder.Services
            .AddScoped<THandler>()
            .AddScoped<BatchMessageHandler<TMessage>, THandler>()
            .AddSingleton(new ServiceBusMessageBatchHandlerBackgroundService<TMessage>.BatchQueueSettings(
                resolvedQueueName,
                providerName,
                resolvedBatchOptions.MaxMessagesBeforeProcessing,
                resolvedBatchOptions.MaxWaitTime))
            .AddHostedService<ServiceBusMessageBatchHandlerBackgroundService<TMessage>>();

        return flowlyBuilder;
    }
}
