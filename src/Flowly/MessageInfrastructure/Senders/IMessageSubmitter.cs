namespace Flowly.MessageInfrastructure.Senders;

internal interface IMessageSubmitter<in TMessage>
{
    Task Submit(TMessage message, CancellationToken cancellationToken = default, string? partitionKey = null);
}