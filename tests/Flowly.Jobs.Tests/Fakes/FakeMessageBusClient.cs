using Flowly.MessagingAbstractions;

namespace Flowly.Jobs.Tests.Fakes;

internal class FakeMessageBusClient : IMessageBusClient
{
    private readonly Dictionary<string, FakeMessageBusSender> _senders = [];

    public string MessagingSystem => "fake";

    public IReadOnlyCollection<string> CreatedSenders => _senders.Keys.ToList();

    public FakeMessageBusSender GetSender(string queueName) => _senders[queueName];

    public Task<IMessageBusSender> CreateMessageBusSender(string queueName)
    {
        var sender = new FakeMessageBusSender();
        _senders[queueName] = sender;
        return Task.FromResult<IMessageBusSender>(sender);
    }

    public Task<IMessageBusReceiver> CreateReceiver(string queueName) => throw new NotSupportedException();
    public Task<IMessageBusProcessor<TMessage>> CreateProcessor<TMessage>(string queueName, MessageBusProcessorOptions options) => throw new NotSupportedException();
    public Task<IExecutionLaneProcessor> CreateExecutionLaneProcessor(string queueName, string laneFilter, MessageBusProcessorOptions options) => throw new NotSupportedException();
    public Task<IDeadLetterReceiver> CreateDeadLetterReceiver(string queueName) => throw new NotSupportedException();
    public Task<long> GetDeadLetterMessageCount(string queueName, CancellationToken cancellationToken = default) => throw new NotSupportedException();
}
