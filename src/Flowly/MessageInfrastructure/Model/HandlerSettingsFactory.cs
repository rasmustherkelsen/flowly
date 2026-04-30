using Flowly.MessageInfrastructure.Receivers;
using Flowly.MessageInfrastructure.Registration;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.MessageInfrastructure.Model;

internal class HandlerSettingsFactory : IHandlerSettingsFactory
{
    public HandlerRegistration<TMessage> Create<THandler, TMessage>(IServiceCollection serviceCollection) where THandler : class
    {
        var providerName = ProviderNameResolver.Resolve(serviceCollection, typeof(TMessage));

        var resolvedQueueOptions = MessageHandlerOptionsResolver.Resolve<THandler, TMessage>();

        var handlerSettings = new HandlerSettings<TMessage>(
            resolvedQueueOptions.QueueName,
            providerName,
            typeof(THandler).Name,
            false,
            resolvedQueueOptions.MaxConcurrentCalls,
            resolvedQueueOptions.MaxRetries,
            resolvedQueueOptions.RetryDelaySeconds,
            resolvedQueueOptions.MaxMessagesBeforeProcessing,
            resolvedQueueOptions.MaxWaitTime);

        var queueRegistration = new DeferredQueueRegistration(
            resolvedQueueOptions.QueueName,
            false,
            resolvedQueueOptions.DefaultMessageTimeToLive,
            resolvedQueueOptions.DeadLetterOnMessageExpiration,
            resolvedQueueOptions.LockDuration);

        return new HandlerRegistration<TMessage>(handlerSettings, queueRegistration);
    }
}