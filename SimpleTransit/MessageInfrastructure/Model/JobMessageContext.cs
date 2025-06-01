using SimpleTransit.MessageInfrastructure.Messages;
using SimpleTransit.MessageInfrastructure.Senders;

namespace SimpleTransit.MessageInfrastructure.Model;

internal class JobMessageContext<T> : MessageContext<T>, IJobMessageContext<T>
{
    private readonly IMessageSender _messageSender;
    private readonly Guid _jobId;

    public JobMessageContext(Guid jobId, T message, IMessageSender messageSender, CancellationToken cancellationToken)
        : base(message, cancellationToken)
    {
        _jobId = jobId;
        _messageSender = messageSender;
    }

    public async Task SaveState<TState>(TState state) where TState : class
    {
        await _messageSender.Send(new UpdateCustomJobState(_jobId, state));
    }
}