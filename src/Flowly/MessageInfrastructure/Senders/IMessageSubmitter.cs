namespace Flowly.MessageInfrastructure.Senders;

public interface IMessageSubmitter<in TMessage>
{
    Task Submit(TMessage message, CancellationToken cancellationToken = default);
}