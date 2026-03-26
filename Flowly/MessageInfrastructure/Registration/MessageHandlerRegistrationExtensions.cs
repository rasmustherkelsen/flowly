using Flowly.MessageInfrastructure.BackgroundServices;
using Flowly.MessageInfrastructure.Model;
using Flowly.MessageInfrastructure.Receivers;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.MessageInfrastructure.Registration;

public static class MessageHandlerRegistrationExtensions
{
    public static IMessageHandlerBuilder<TMessage> AddMessageHandler<TMessage, THandler>(this IFlowlyBuilder flowlyBuilder)
        where THandler : MessageHandlerBase<TMessage>
        where TMessage : class
    {
        var resolvedQueueOptions = HandlerQueueOptionsResolver.Resolve<THandler, TMessage>();
        var resolvedQueueName = resolvedQueueOptions.QueueName;

        flowlyBuilder.AddQueueRegistration(new DeferredQueueRegistration(
            resolvedQueueName,
            false,
            resolvedQueueOptions.DefaultMessageTimeToLive,
            resolvedQueueOptions.DeadLetterOnMessageExpiration,
            resolvedQueueOptions.LockDuration));

        var services = flowlyBuilder.Services
            .AddScoped<THandler>()
            .AddScoped<MessageHandlerBase<TMessage>, THandler>();

        services
            .AddSingleton(new HandlerSettings<TMessage>(resolvedQueueName, typeof(THandler).Name, false, resolvedQueueOptions.MaxConcurrentCalls, resolvedQueueOptions.MaxRetries, resolvedQueueOptions.RetryDelaySeconds))
            .AddHostedService<ServiceBusMessageHandlerBackgroundService<TMessage>>();

        return new MessageHandlerBuilder<TMessage>(flowlyBuilder, resolvedQueueName);
    }

    public static IFlowlyBuilder AddBatchMessageHandler<TMessage, THandler>(this IFlowlyBuilder flowlyBuilder)
        where THandler : BatchMessageHandlerBase<TMessage>
        where TMessage : class
    {
        var resolvedQueueOptions = HandlerQueueOptionsResolver.Resolve<THandler, TMessage>();
        var resolvedBatchOptions = BatchMessageHandlerOptionsResolver.Resolve<THandler>();
        var resolvedQueueName = resolvedQueueOptions.QueueName;

        flowlyBuilder.AddQueueRegistration(new DeferredQueueRegistration(
            resolvedQueueName,
            false,
            resolvedQueueOptions.DefaultMessageTimeToLive,
            resolvedQueueOptions.DeadLetterOnMessageExpiration,
            resolvedQueueOptions.LockDuration));

        flowlyBuilder.Services
            .AddScoped<THandler>()
            .AddScoped<BatchMessageHandlerBase<TMessage>, THandler>()
            .AddSingleton(new ServiceBusMessageBatchHandlerBackgroundService<TMessage>.BatchQueueSettings(
                resolvedQueueName,
                resolvedBatchOptions.MaxMessagesBeforeProcessing,
                resolvedBatchOptions.MaxWaitTime))
            .AddHostedService<ServiceBusMessageBatchHandlerBackgroundService<TMessage>>();
        
        return flowlyBuilder;
    }
}