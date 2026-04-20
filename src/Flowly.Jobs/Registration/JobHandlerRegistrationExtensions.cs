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
    public static IFlowlyBuilder AddJobHandler<TMessage, THandler>(this IFlowlyBuilder flowlyBuilder)
        where THandler : JobMessageHandlerBase<TMessage>
        where TMessage : class, IJobMessage
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
            .AddScoped<JobMessageHandlerBase<TMessage>, THandler>()
            .AddSingleton(new HandlerSettings<TMessage>(resolvedQueueName, providerName, typeof(THandler).Name, true, resolvedQueueOptions.MaxConcurrentCalls, resolvedQueueOptions.MaxRetries, resolvedQueueOptions.RetryDelaySeconds))
            .AddHostedService<JobHandlerBackgroundService<TMessage>>();

        flowlyBuilder.AddJobStateSubmitters();

        return flowlyBuilder;
    }
}
