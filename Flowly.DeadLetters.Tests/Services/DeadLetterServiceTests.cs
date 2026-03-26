using Flowly.DeadLetters.DatabaseModel;
using Flowly.DeadLetters.Repositories;
using Flowly.DeadLetters.Services;
using Flowly.MessagingAbstractions;

namespace Flowly.DeadLetters.Tests.Services;

public class DeadLetterServiceTests
{
    public class Requeue
    {
        [Fact]
        public async Task ThrowsKeyNotFound_WhenMessageIdDoesNotExist()
        {
            var repository = new FakeDeadLetterRepository();
            var sut = new DeadLetterService(repository, new FakeMessageBusClient());

            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                sut.Requeue("nonexistent-id"));
        }

        [Fact]
        public async Task ThrowsInvalidOperation_WhenStatusIsAlreadyRequeued()
        {
            var repository = new FakeDeadLetterRepository();
            repository.Add(BuildDeadLetter("msg-1", DeadLetterStatus.Requeued));
            var sut = new DeadLetterService(repository, new FakeMessageBusClient());

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                sut.Requeue("msg-1"));
        }

        [Fact]
        public async Task SendsRawMessageToOriginalQueue()
        {
            var repository = new FakeDeadLetterRepository();
            repository.Add(BuildDeadLetter("msg-1", DeadLetterStatus.Pending, queueName: "my-queue", body: "hello"));
            var messageBusClient = new FakeMessageBusClient();
            var sut = new DeadLetterService(repository, messageBusClient);

            await sut.Requeue("msg-1");

            var sender = messageBusClient.GetSender("my-queue");
            Assert.Single(sender.SentRawMessages);
            Assert.Equal("hello", sender.SentRawMessages[0].RawBody);
        }

        [Fact]
        public async Task SendsApplicationPropertiesWithMessage()
        {
            var repository = new FakeDeadLetterRepository();
            repository.Add(BuildDeadLetter(
                "msg-1",
                DeadLetterStatus.Pending,
                properties: """{"flowly-retry-count":2,"source":"test"}"""));
            var messageBusClient = new FakeMessageBusClient();
            var sut = new DeadLetterService(repository, messageBusClient);

            await sut.Requeue("msg-1");

            var sender = messageBusClient.GetSender("test-queue");
            var sent = sender.SentRawMessages[0];
            Assert.Equal(2, sent.ApplicationProperties["flowly-retry-count"]);
            Assert.Equal("test", sent.ApplicationProperties["source"]);
        }

        [Fact]
        public async Task MarksMessageAsRequeued()
        {
            var repository = new FakeDeadLetterRepository();
            repository.Add(BuildDeadLetter("msg-1", DeadLetterStatus.Pending));
            var sut = new DeadLetterService(repository, new FakeMessageBusClient());

            await sut.Requeue("msg-1", requeuedBy: "admin");

            Assert.Equal("msg-1", repository.RequeuedMessageId);
            Assert.Equal("admin", repository.RequeuedBy);
        }
    }

    public class Discard
    {
        [Fact]
        public async Task ThrowsKeyNotFound_WhenMessageIdDoesNotExist()
        {
            var repository = new FakeDeadLetterRepository();
            var sut = new DeadLetterService(repository, new FakeMessageBusClient());

            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                sut.Discard("nonexistent-id"));
        }

        [Fact]
        public async Task ThrowsInvalidOperation_WhenStatusIsAlreadyRequeued()
        {
            var repository = new FakeDeadLetterRepository();
            repository.Add(BuildDeadLetter("msg-1", DeadLetterStatus.Requeued));
            var sut = new DeadLetterService(repository, new FakeMessageBusClient());

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                sut.Discard("msg-1"));
        }

        [Fact]
        public async Task DeletesRecord()
        {
            var repository = new FakeDeadLetterRepository();
            repository.Add(BuildDeadLetter("msg-1", DeadLetterStatus.Pending));
            var sut = new DeadLetterService(repository, new FakeMessageBusClient());

            await sut.Discard("msg-1");

            Assert.Equal("msg-1", repository.DeletedMessageId);
        }

        [Fact]
        public async Task DoesNotSendAnyMessages()
        {
            var repository = new FakeDeadLetterRepository();
            repository.Add(BuildDeadLetter("msg-1", DeadLetterStatus.Pending));
            var messageBusClient = new FakeMessageBusClient();
            var sut = new DeadLetterService(repository, messageBusClient);

            await sut.Discard("msg-1");

            Assert.Empty(messageBusClient.CreatedSenders);
        }
    }

    private static DeadLetter BuildDeadLetter(
        string messageId,
        DeadLetterStatus status = DeadLetterStatus.Pending,
        string queueName = "test-queue",
        string body = "{}",
        string properties = "{}")
        => new()
        {
            MessageId = messageId,
            QueueName = queueName,
            MessageBody = body,
            MessageProperties = properties,
            DeadLetteredAt = DateTimeOffset.UtcNow,
            Status = status
        };

    private class FakeDeadLetterRepository : IDeadLetterRepository
    {
        private readonly Dictionary<string, DeadLetter> _store = [];

        public string? RequeuedMessageId { get; private set; }
        public string? RequeuedBy { get; private set; }
        public string? DeletedMessageId { get; private set; }

        public void Add(DeadLetter deadLetter) => _store[deadLetter.MessageId] = deadLetter;

        public Task SaveBatch(IReadOnlyCollection<IDeadLetterMessage> messages, string queueName, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<DateTimeOffset?> GetLastIngestionTime(string queueName, CancellationToken cancellationToken = default)
            => Task.FromResult<DateTimeOffset?>(null);

        public Task<DeadLetter?> Get(string messageId, CancellationToken cancellationToken = default)
            => Task.FromResult(_store.GetValueOrDefault(messageId));

        public Task MarkAsRequeued(string messageId, string? requeuedBy, CancellationToken cancellationToken = default)
        {
            RequeuedMessageId = messageId;
            RequeuedBy = requeuedBy;
            return Task.CompletedTask;
        }

        public Task Delete(string messageId, CancellationToken cancellationToken = default)
        {
            DeletedMessageId = messageId;
            return Task.CompletedTask;
        }
    }

    private class FakeMessageBusClient : IMessageBusClient
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

    private class FakeMessageBusSender : IMessageBusSender
    {
        public List<(string RawBody, IReadOnlyDictionary<string, object> ApplicationProperties)> SentRawMessages { get; } = [];

        public Task SendMessage<TMessage>(TMessage message, MessageProperties messageProperties, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task SendEmptyMessage(MessageProperties messageProperties, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task SendRawMessage(string rawBody, IReadOnlyDictionary<string, object> applicationProperties, CancellationToken cancellationToken = default)
        {
            SentRawMessages.Add((rawBody, applicationProperties));
            return Task.CompletedTask;
        }
    }
}
