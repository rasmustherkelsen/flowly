using Flowly.MessageInfrastructure.Model;

namespace Flowly.MessageInfrastructure.Receivers;

public interface IBatchMessageHandler<T>
{
    Task Handle(IBatchMessageContext<T> messageContext);
}
