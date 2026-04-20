using Flowly.MessageInfrastructure.BackgroundServices;
using Flowly.MessageInfrastructure.Model;
using Flowly.MessageInfrastructure.Receivers;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.MessageInfrastructure.Registration;

public static class MessageHandlerRegistrationExtensions
{
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

        flowlyBuilder.Services
            .AddScoped<THandler>()
            .AddScoped<MessageHandler<TMessage>, THandler>()
            .AddSingleton(new HandlerSettings<TMessage>(resolvedQueueName, providerName, typeof(THandler).Name, false, resolvedQueueOptions.MaxConcurrentCalls, resolvedQueueOptions.MaxRetries, resolvedQueueOptions.RetryDelaySeconds))
            .AddHostedService<ServiceBusMessageHandlerBackgroundService<TMessage>>();

        return new MessageHandlerBuilder<TMessage>(flowlyBuilder, resolvedQueueName, providerName);
    }

    public static IFlowlyBuilder AddBatchMessageHandler<TMessage, THandler>(this IFlowlyBuilder flowlyBuilder)
        where THandler : BatchMessageHandlerBase<TMessage>
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
            .AddScoped<BatchMessageHandlerBase<TMessage>, THandler>()
            .AddSingleton(new ServiceBusMessageBatchHandlerBackgroundService<TMessage>.BatchQueueSettings(
                resolvedQueueName,
                providerName,
                resolvedBatchOptions.MaxMessagesBeforeProcessing,
                resolvedBatchOptions.MaxWaitTime))
            .AddHostedService<ServiceBusMessageBatchHandlerBackgroundService<TMessage>>();

        return flowlyBuilder;
    }
}
