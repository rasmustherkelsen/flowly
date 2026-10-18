using Flowly.Transport;

namespace Flowly.Tests.Fakes;

internal sealed class FakeMessageBusSender : IMessageBusSender
{
    public List<(object Message, MessageProperties Properties)> SentMessages { get; } = [];

    public Task SendMessage<TMessage>(TMessage message, MessageProperties messageProperties, CancellationToken cancellationToken = default)
    {
        SentMessages.Add((message!, messageProperties));
        return Task.CompletedTask;
    }

    public Task SendEmptyMessage(MessageProperties messageProperties, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task SendRawMessage(string rawBody, IReadOnlyDictionary<string, object> applicationProperties, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
