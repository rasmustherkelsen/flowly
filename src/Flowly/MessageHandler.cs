namespace Flowly;

public abstract class MessageHandler<TMessage>
{
    public virtual void Configure(HandlerQueueOptions options)
    {
    }

    public abstract Task Handle(IMessageContext<TMessage> messageContext);
}
