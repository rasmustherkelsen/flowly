using Flowly.MessageInfrastructure.Model;

namespace Flowly.MessageInfrastructure.Receivers;

public interface IMessageHandler<T>
{
    Task Handle(IMessageContext<T> messageContext);
}
