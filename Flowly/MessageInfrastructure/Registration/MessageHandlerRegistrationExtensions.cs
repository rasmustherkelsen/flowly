using Flowly.MessageInfrastructure.BackgroundServices;
using Flowly.MessageInfrastructure.Model;
using Flowly.MessageInfrastructure.Receivers;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.MessageInfrastructure.Registration;

public static class MessageHandlerRegistrationExtensions
{
    public static IServiceCollection AddMessageHandler<TMessage, THandler>(this IServiceCollection services, string queueName, int maxConcurrentCalls = 1)
        where THandler : class, IMessageHandler<TMessage>
        where TMessage : class
    {
        services.AddSingleton(new DeferredQueueRegistration(queueName));

        services
            .AddScoped<IMessageHandler<TMessage>, THandler>()
            .AddSingleton(new HandlerSettings<TMessage>(queueName, typeof(THandler).Name, false, maxConcurrentCalls))
            .AddHostedService<ServiceBusMessageHandlerBackgroundService<TMessage>>();

        return services;
    }

    public static IServiceCollection AddBatchMessageHandler<TMessage, THandler>(this IServiceCollection services, string queueName, int maxMessagesBeforeProcessing, TimeSpan maxWaitTime)
        where THandler : class, IBatchMessageHandler<TMessage>
        where TMessage : class
    {
        services.AddSingleton(new DeferredQueueRegistration(queueName));

        services
            .AddScoped<IBatchMessageHandler<TMessage>, THandler>()
            .AddSingleton(new ServiceBusMessageBatchHandlerBackgroundService<TMessage>.BatchQueueSettings(queueName, maxMessagesBeforeProcessing, maxWaitTime))
            .AddHostedService<ServiceBusMessageBatchHandlerBackgroundService<TMessage>>();
        return services;
    }
}