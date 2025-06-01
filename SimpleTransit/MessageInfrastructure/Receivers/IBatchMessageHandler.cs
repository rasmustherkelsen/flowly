using SimpleTransit.MessageInfrastructure.Model;

namespace SimpleTransit.MessageInfrastructure.Receivers;

public interface IBatchMessageHandler<T>
{
    Task Handle(IBatchMessageContext<T> messageContext);
}
