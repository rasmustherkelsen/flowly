using Flowly.Jobs.Messages;

namespace Flowly.Jobs.Model;

internal class JobMessageContext<T>(
    JobId jobId,
    T message,
    IMessageSender messageSender,
    CancellationToken cancellationToken,
    MessageProperties? properties = null) : IMessageContext<T>, IJobMessageContext<T>
{
    public async Task SaveState<TState>(TState state) where TState : class
        => await messageSender.Send(new FlowlysysUpdateJobCustomStateMessage(jobId, state));

    public T Message => message;

    public CancellationToken CancellationToken => cancellationToken;

    public MessageProperties Properties { get; } = properties ?? MessageProperties.Empty;
}
