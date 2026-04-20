using Flowly.MessagingAbstractions;

namespace Flowly.Jobs.Tests.Fakes;

internal class FakeMessageBusSender : IMessageBusSender
{
    public List<(object Message, MessageProperties Properties)> SentMessages { get; } = [];
    public List<MessageProperties> SentEmptyMessages { get; } = [];
    public List<(string RawBody, IReadOnlyDictionary<string, object> ApplicationProperties)> SentRawMessages { get; } = [];

    public Task SendMessage<TMessage>(TMessage message, MessageProperties messageProperties, CancellationToken cancellationToken = default)
    {
        SentMessages.Add((message!, messageProperties));
        return Task.CompletedTask;
    }

    public Task SendEmptyMessage(MessageProperties messageProperties, CancellationToken cancellationToken = default)
    {
        SentEmptyMessages.Add(messageProperties);
        return Task.CompletedTask;
    }

    public Task SendRawMessage(string rawBody, IReadOnlyDictionary<string, object> applicationProperties, CancellationToken cancellationToken = default)
    {
        SentRawMessages.Add((rawBody, applicationProperties));
        return Task.CompletedTask;
    }
}
