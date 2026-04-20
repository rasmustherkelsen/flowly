namespace Flowly.MessageInfrastructure.Registration;

public interface IMessageHandlerBuilder<TMessage> : IFlowlyBuilder
{
    string QueueName { get; }
    string ProviderName { get; }
}