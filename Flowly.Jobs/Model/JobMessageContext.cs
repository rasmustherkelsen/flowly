using Flowly.Jobs.Messages;
using Flowly.MessageInfrastructure.Model;
using Flowly.MessageInfrastructure.Senders;

namespace Flowly.Jobs.Model;

internal class JobMessageContext<T>(Guid jobId, T message, IMessageSender messageSender, CancellationToken cancellationToken) : IMessageContext<T>, IJobMessageContext<T>
{
    public async Task SaveState<TState>(TState state) where TState : class
        => await messageSender.Send(new UpdateCustomJobState(jobId, state));

    public T Message => message;

    public CancellationToken CancellationToken => cancellationToken;
}
