using Flowly.Jobs.Model;

namespace Flowly.Jobs.Receivers;

public abstract class JobMessageHandlerBase<TMessage> where TMessage : IJobMessage
{
    public virtual void Configure(HandlerQueueOptions options)
    {
    }

    public abstract Task Handle(IJobMessageContext<TMessage> messageContext);
}
