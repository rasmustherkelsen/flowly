using Flowly.MessageInfrastructure.Model;

namespace Flowly.MessageInfrastructure.Receivers;

public abstract class BatchMessageHandlerBase<TMessage>
{
    public virtual void Configure(HandlerQueueOptions options)
    {
    }

    public virtual void Configure(BatchMessageHandlerOptions options)
    {
    }

    public abstract Task Handle(IBatchMessageContext<TMessage> messageContext);
}
