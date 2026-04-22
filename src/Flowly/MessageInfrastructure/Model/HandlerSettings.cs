using Flowly.MessageInfrastructure.Receivers;
using Flowly.MessageInfrastructure.Registration;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.MessageInfrastructure.Model;

public interface IHandlerSettings<TMesssage>
{
    string QueueName { get; }

    string ProviderName { get; }

    string HandlerName { get; }

    bool ReadAndDelete { get; }

    int MaxConcurrentCalls { get; }

    int MaxRetries { get; }

    int RetryDelaySeconds { get; }
}

public record HandlerSettings<TMessage>(
    string QueueName,
    string ProviderName,
    string HandlerName,
    bool ReadAndDelete,
    int MaxConcurrentCalls = 1,
    int MaxRetries = 0,
    int RetryDelaySeconds = 0) : IHandlerSettings<TMessage>;

public record HandlerRegistration<TMessage>(
    IHandlerSettings<TMessage> HandlerSettings,
    DeferredQueueRegistration QueueRegistration);

public interface IHandlerSettingsFactory
{
    HandlerRegistration<TMessage> Create<THandler, TMessage>(IServiceCollection serviceCollection) where THandler : class;
}

internal class HandlerSettingsFactory : IHandlerSettingsFactory
{
    public HandlerRegistration<TMessage> Create<THandler, TMessage>(IServiceCollection serviceCollection) where THandler : class
    {
        var providerName = ProviderNameResolver.Resolve(serviceCollection, typeof(TMessage));
        var resolvedQueueOptions = HandlerQueueOptionsResolver.Resolve<THandler, TMessage>();

        var handlerSettings = new HandlerSettings<TMessage>(
            resolvedQueueOptions.QueueName,
            providerName,
            typeof(THandler).Name,
            true,
            resolvedQueueOptions.MaxConcurrentCalls,
            resolvedQueueOptions.MaxRetries,
            resolvedQueueOptions.RetryDelaySeconds);

        var queueRegistration = new DeferredQueueRegistration(
            resolvedQueueOptions.QueueName,
            RequiresSession: false,
            resolvedQueueOptions.DefaultMessageTimeToLive,
            resolvedQueueOptions.DeadLetterOnMessageExpiration,
            resolvedQueueOptions.LockDuration);

        return new HandlerRegistration<TMessage>(handlerSettings, queueRegistration);
    }
}