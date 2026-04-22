using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.MessageInfrastructure.Events.Registration;

internal class EventHandlerBuilder<TEvent>(
    IFlowlyBuilder inner,
    string topicOrExchangeName,
    string subscriptionName,
    string providerName) : IEventHandlerBuilder<TEvent>
    where TEvent : class
{
    public IServiceCollection Services => inner.Services;

    public IConfiguration Configuration => inner.Configuration;

    public string TopicName { get; } = topicOrExchangeName;

    public string SubscriptionName { get; } = subscriptionName;

    public string ProviderName { get; } = providerName;
}