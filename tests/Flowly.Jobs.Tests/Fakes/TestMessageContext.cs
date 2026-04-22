namespace Flowly.Jobs.Tests.Fakes;

internal class TestMessageContext<T>(T message, CancellationToken cancellationToken = default) : IMessageContext<T>
{
    public T Message { get; } = message;

    public CancellationToken CancellationToken { get; } = cancellationToken;
}
