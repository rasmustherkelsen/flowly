using SimpleTransit.MessageInfrastructure.Model;

namespace SimpleTransit.MessageInfrastructure.Receivers;

public interface IMessageHandler<T>
{
    Task Handle(IMessageContext<T> messageContext);
}
