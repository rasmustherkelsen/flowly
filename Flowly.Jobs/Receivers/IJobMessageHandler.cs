using Flowly.Jobs.Model;

namespace Flowly.Jobs.Receivers;

public interface IJobMessageHandler<T> where T : IJobMessage
{
    Task Handle(IJobMessageContext<T> messageContext);
}
