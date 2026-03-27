using Flowly.MessagingAbstractions;

namespace Flowly.DeadLetters.Tests.Fakes;

internal class FakeMessageBusClient : IMessageBusClient
{
    private readonly Dictionary<string, FakeMessageBusSender> _senders = [];

    public IReadOnlyCollection<string> CreatedSenders => _senders.Keys.ToList();

    public FakeMessageBusSender GetSender(string queueName) => _senders[queueName];

    public IMessageBusSender CreateMessageBusSender(string queueName)
    {
        var sender = new FakeMessageBusSender();
        _senders[queueName] = sender;
        return sender;
    }

    public IMessageBusReceiver CreateReceiver(string queueName) => throw new NotSupportedException();
    public IMessageBusProcessor<TMessage> CreateProcessor<TMessage>(string queueName, MessageBusProcessorOptions options) => throw new NotSupportedException();
    public IExecutionLaneProcessor CreateExecutionLaneProcessor(string queueName, string laneFilter, MessageBusProcessorOptions options) => throw new NotSupportedException();
    public IDeadLetterReceiver CreateDeadLetterReceiver(string queueName) => throw new NotSupportedException();
    public Task<long> GetDeadLetterMessageCount(string queueName, CancellationToken cancellationToken = default) => throw new NotSupportedException();
}
