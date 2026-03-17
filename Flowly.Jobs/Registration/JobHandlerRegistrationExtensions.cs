using Flowly.Jobs.BackgroundServices;
using Flowly.Jobs.Model;
using Flowly.Jobs.Receivers;
using Flowly.MessageInfrastructure.Model;
using Flowly.MessageInfrastructure.Receivers;
using Flowly.MessageInfrastructure.Registration;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.Jobs.Registration;

public static class JobHandlerRegistrationExtensions
{
    public static IFlowlyBuilder AddJobHandler<TMessage, THandler>(this IFlowlyBuilder flowlyBuilder, int maxConcurrentCalls = 1)
        where THandler : JobMessageHandlerBase<TMessage>
        where TMessage : class, IJobMessage
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
            .AddScoped<JobMessageHandlerBase<TMessage>, THandler>();

        services
            .AddSingleton(new HandlerSettings<TMessage>(resolvedQueueName, typeof(THandler).Name, true, maxConcurrentCalls))
            .AddHostedService<JobHandlerBackgroundService<TMessage>>();
            
        flowlyBuilder.AddJobStateSubmitters();

        return flowlyBuilder;
    }
}