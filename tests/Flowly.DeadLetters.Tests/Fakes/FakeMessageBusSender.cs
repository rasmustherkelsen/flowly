using Flowly.Transport;

namespace Flowly.DeadLetters.Tests.Fakes;

internal class FakeMessageBusSender : IMessageBusSender
{
    public Exception? FailureToThrowOnSendRawMessage { get; init; }

    public List<(string RawBody, IReadOnlyDictionary<string, object> ApplicationProperties)> SentRawMessages { get; } = [];

    public Task SendMessage<TMessage>(TMessage message, MessageProperties messageProperties, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task SendEmptyMessage(MessageProperties messageProperties, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task SendRawMessage(string rawBody, IReadOnlyDictionary<string, object> applicationProperties, CancellationToken cancellationToken = default)
    {
        if (FailureToThrowOnSendRawMessage is not null)
        {
            throw FailureToThrowOnSendRawMessage;
        }

        SentRawMessages.Add((rawBody, applicationProperties));
        return Task.CompletedTask;
    }
}