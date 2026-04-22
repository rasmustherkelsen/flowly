namespace Flowly;

public interface IReceivedMessage
{
    public MessageProperties Properties { get; }
    public TBody GetBody<TBody>();
}

public interface IReceivedMessage<TMessage>
{
    TMessage Body { get; }

    MessageProperties Properties { get; }

    Task Complete(CancellationToken cancellationToken = default);

    Task DeadLetter(string? reason = null, CancellationToken cancellationToken = default);
}