namespace Flowly.MessagingAbstractions;

public interface IReceivedMessage<TMessage>
{
    public TMessage Body { get; }
    
    public MessageProperties Properties { get; }
}