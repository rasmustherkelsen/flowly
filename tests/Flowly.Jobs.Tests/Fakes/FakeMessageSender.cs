using Flowly.MessageInfrastructure.Senders;

namespace Flowly.Jobs.Tests.Fakes;

internal class FakeMessageSender : IMessageSender
{
    public List<object> SentMessages { get; } = [];

    public Task Send<TMessage>(TMessage message, CancellationToken cancellationToken = default)
    {
        SentMessages.Add(message!);
        return Task.CompletedTask;
    }
}
