using Flowly.MessageInfrastructure.Model;

namespace Flowly.MessageInfrastructure.Receivers;

public abstract class MessageHandlerBase<TMessage>
{
    public virtual void Configure(HandlerQueueOptions options)
    {
    }

    public abstract Task Handle(IMessageContext<TMessage> messageContext);
}
