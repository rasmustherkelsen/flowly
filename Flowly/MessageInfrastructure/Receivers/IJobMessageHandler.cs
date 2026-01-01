using Flowly.MessageInfrastructure.Model;

namespace Flowly.MessageInfrastructure.Receivers;

public interface IJobMessageHandler<T> where T : IJobMessage
{
    Task Handle(IJobMessageContext<T> messageContext);
}
