using Flowly.MessageInfrastructure.Registration;

namespace Flowly.MessageInfrastructure.Events.Registration;

public interface IEventHandlerBuilder<TEvent> : IFlowlyBuilder
    where TEvent : class
{
    string TopicOrExchangeName { get; }
    string SubscriptionName { get; }
    string ProviderName { get; }
}
