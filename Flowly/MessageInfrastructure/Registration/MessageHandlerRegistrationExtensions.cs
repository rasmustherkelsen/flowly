using Flowly.MessageInfrastructure.BackgroundServices;
using Flowly.MessageInfrastructure.Model;
using Flowly.MessageInfrastructure.Receivers;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.MessageInfrastructure.Registration;

public static class MessageHandlerRegistrationExtensions
{
    public static IFlowlyBuilder AddMessageHandler<TMessage, THandler>(this IFlowlyBuilder flowlyBuilder, int maxConcurrentCalls = 1)
        where THandler : MessageHandlerBase<TMessage>
        where TMessage : class
    {
        var resolvedQueueOptions = HandlerQueueOptionsResolver.Resolve<THandler>();
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
            .AddSingleton(new HandlerSettings<TMessage>(resolvedQueueName, typeof(THandler).Name, false, maxConcurrentCalls))
            .AddHostedService<ServiceBusMessageHandlerBackgroundService<TMessage>>();

        return flowlyBuilder;
    }

    public static IFlowlyBuilder AddBatchMessageHandler<TMessage, THandler>(this IFlowlyBuilder flowlyBuilder, int maxMessagesBeforeProcessing, TimeSpan maxWaitTime)
        where THandler : BatchMessageHandlerBase<TMessage>
        where TMessage : class
    {
        var resolvedQueueOptions = HandlerQueueOptionsResolver.Resolve<THandler>();
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
            .AddSingleton(new ServiceBusMessageBatchHandlerBackgroundService<TMessage>.BatchQueueSettings(resolvedQueueName, maxMessagesBeforeProcessing, maxWaitTime))
            .AddHostedService<ServiceBusMessageBatchHandlerBackgroundService<TMessage>>();
        
        return flowlyBuilder;
    }
}