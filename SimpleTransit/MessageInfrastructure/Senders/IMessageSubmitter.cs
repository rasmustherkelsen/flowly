namespace SimpleTransit.MessageInfrastructure.Senders;

internal interface IMessageSubmitter<in TMessage>
{
    Task Submit(TMessage message, CancellationToken cancellationToken = default);
}