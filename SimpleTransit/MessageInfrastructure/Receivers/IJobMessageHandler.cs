using SimpleTransit.MessageInfrastructure.Model;

namespace SimpleTransit.MessageInfrastructure.Receivers;

public interface IJobMessageHandler<T> where T : IJobMessage
{
    Task Handle(IJobMessageContext<T> messageContext);
}
