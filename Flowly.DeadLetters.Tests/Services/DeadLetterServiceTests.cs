using Flowly.DeadLetters.BackgroundServices;
using Flowly.DeadLetters.DatabaseModel;
using Flowly.DeadLetters.Services;
using Flowly.DeadLetters.Tests.Fakes;

namespace Flowly.DeadLetters.Tests.Services;

public class DeadLetterServiceTests
{
    public class Requeue
    {
        [Fact]
        public async Task ThrowsKeyNotFound_WhenMessageIdDoesNotExist()
        {
            var repository = new FakeDeadLetterRepository();
            var deadLetterService = BuildService(repository, new FakeMessageBusClient());

            await Assert.ThrowsAsync<KeyNotFoundException>(() => deadLetterService.Requeue("nonexistent-id"));
        }

        [Fact]
        public async Task ThrowsInvalidOperation_WhenStatusIsAlreadyRequeued()
        {
            var repository = new FakeDeadLetterRepository();
            repository.Add(BuildDeadLetter("msg-1", DeadLetterStatus.Requeued));
            var deadLetterService = BuildService(repository, new FakeMessageBusClient());

            await Assert.ThrowsAsync<InvalidOperationException>(() => deadLetterService.Requeue("msg-1"));
        }

        [Fact]
        public async Task SendsRawMessageToOriginalQueue()
        {
            var repository = new FakeDeadLetterRepository();
            repository.Add(BuildDeadLetter("msg-1", DeadLetterStatus.Pending, queueName: "my-queue", body: "hello"));
            var messageBusClient = new FakeMessageBusClient();
            var deadLetterService = BuildService(repository, messageBusClient);

            await deadLetterService.Requeue("msg-1");

            var sender = messageBusClient.GetSender("my-queue");
            Assert.Single(sender.SentRawMessages);
            Assert.Equal("hello", sender.SentRawMessages[0].RawBody);
        }

        [Fact]
        public async Task SendsApplicationPropertiesWithMessage()
        {
            var repository = new FakeDeadLetterRepository();
            repository.Add(BuildDeadLetter("msg-1", DeadLetterStatus.Pending, properties: """{"flowly-retry-count":2,"source":"test"}"""));
            var messageBusClient = new FakeMessageBusClient();
            var deadLetterService = BuildService(repository, messageBusClient);

            await deadLetterService.Requeue("msg-1");

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
            var deadLetterService = BuildService(repository, new FakeMessageBusClient());

            await deadLetterService.Requeue("msg-1", requeuedBy: "admin");

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
            var deadLetterService = BuildService(repository, new FakeMessageBusClient());

            await Assert.ThrowsAsync<KeyNotFoundException>(() => deadLetterService.Discard("nonexistent-id"));
        }

        [Fact]
        public async Task ThrowsInvalidOperation_WhenStatusIsAlreadyRequeued()
        {
            var repository = new FakeDeadLetterRepository();
            repository.Add(BuildDeadLetter("msg-1", DeadLetterStatus.Requeued));
            var deadLetterService = BuildService(repository, new FakeMessageBusClient());

            await Assert.ThrowsAsync<InvalidOperationException>(() => deadLetterService.Discard("msg-1"));
        }

        [Fact]
        public async Task DeletesRecord()
        {
            var repository = new FakeDeadLetterRepository();
            repository.Add(BuildDeadLetter("msg-1", DeadLetterStatus.Pending));
            var deadLetterService = BuildService(repository, new FakeMessageBusClient());

            await deadLetterService.Discard("msg-1");

            Assert.Equal("msg-1", repository.DeletedMessageId);
        }

        [Fact]
        public async Task DoesNotSendAnyMessages()
        {
            var repository = new FakeDeadLetterRepository();
            repository.Add(BuildDeadLetter("msg-1", DeadLetterStatus.Pending));
            var messageBusClient = new FakeMessageBusClient();
            var deadLetterService = BuildService(repository, messageBusClient);

            await deadLetterService.Discard("msg-1");

            Assert.Empty(messageBusClient.CreatedSenders);
        }
    }

    private static DeadLetterService BuildService(FakeDeadLetterRepository repository, FakeMessageBusClient client) =>
        new(repository, new FakeMessageBusClientRegistry(client), [new DeadLetterIngestionSettings("test-queue", "azure-service-bus")]);

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
}
