namespace Flowly.MessagingAbstractions;

public interface IReceivedMessage
{
    public TBody GetBody<TBody>();
    
    public MessageProperties Properties { get; }
}

public interface IReceivedMessage<TMessage>
{
    TMessage Body { get; }

    MessageProperties Properties { get; }

    Task Complete(CancellationToken cancellationToken = default);

    Task DeadLetter(string? reason = null, CancellationToken cancellationToken = default);
}