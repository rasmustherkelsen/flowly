using Flowly.MessageInfrastructure.Model;

namespace Flowly;

public abstract class BatchMessageHandler<TMessage>
{
    public virtual void Configure(HandlerQueueOptions options)
    {
    }

    public virtual void Configure(BatchMessageHandlerOptions options)
    {
    }

    public abstract Task Handle(IBatchMessageContext<TMessage> messageContext);
}