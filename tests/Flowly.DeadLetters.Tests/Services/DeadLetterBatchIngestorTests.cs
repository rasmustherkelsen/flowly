using Flowly.DeadLetters.Repositories;
using Flowly.DeadLetters.Services;
using Flowly.DeadLetters.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Flowly.DeadLetters.Tests.Services;

public class DeadLetterBatchIngestorTests
{
    public class IngestForQueue
    {
        [Fact]
        public async Task OnSuccess_CompletesEachMessage()
        {
            var repository = new FakeDeadLetterRepository();
            var receiver = new FakeDeadLetterReceiver();
            var ingestor = BuildIngestor(repository);
            var messages = new FakeDeadLetterMessage[] { new() { MessageId = "msg-1" }, new() { MessageId = "msg-2" } };

            await ingestor.IngestForQueue(receiver, messages, "orders", CancellationToken.None);

            Assert.Equal(2, receiver.CompletedMessages.Count);
            Assert.Empty(receiver.AbandonedMessages);
        }

        [Fact]
        public async Task OnSuccess_SavesBatchToRepositoryUnderTheQueueName()
        {
            var repository = new FakeDeadLetterRepository();
            var receiver = new FakeDeadLetterReceiver();
            var ingestor = BuildIngestor(repository);
            var messages = new FakeDeadLetterMessage[] { new() { MessageId = "msg-1" } };

            await ingestor.IngestForQueue(receiver, messages, "orders", CancellationToken.None);

            Assert.Equal("orders", repository.SavedQueueName);
            Assert.Single(repository.SavedMessages);
        }

        [Fact]
        public async Task WhenRepositoryThrows_AbandonsEachMessage()
        {
            var repository = new FakeDeadLetterRepository { ThrowOnSaveBatch = true };
            var receiver = new FakeDeadLetterReceiver();
            var ingestor = BuildIngestor(repository);
            var messages = new FakeDeadLetterMessage[] { new() { MessageId = "msg-1" }, new() { MessageId = "msg-2" } };

            await ingestor.IngestForQueue(receiver, messages, "orders", CancellationToken.None);

            Assert.Equal(2, receiver.AbandonedMessages.Count);
            Assert.Empty(receiver.CompletedMessages);
        }

        [Fact]
        public async Task WhenRepositoryThrows_DoesNotThrow()
        {
            var repository = new FakeDeadLetterRepository { ThrowOnSaveBatch = true };
            var receiver = new FakeDeadLetterReceiver();
            var ingestor = BuildIngestor(repository);

            await ingestor.IngestForQueue(receiver, [new FakeDeadLetterMessage { MessageId = "msg-1" }], "orders", CancellationToken.None);
        }

        [Fact]
        public async Task WhenCompleteMessageThrowsPartway_DoesNotAbandonAlreadyCompletedMessages()
        {
            var repository = new FakeDeadLetterRepository();
            var receiver = new FakeDeadLetterReceiver { MessageIdToThrowOnComplete = "msg-2" };
            var ingestor = BuildIngestor(repository);
            var messages = new FakeDeadLetterMessage[] { new() { MessageId = "msg-1" }, new() { MessageId = "msg-2" }, new() { MessageId = "msg-3" } };

            await ingestor.IngestForQueue(receiver, messages, "orders", CancellationToken.None);

            Assert.Equal(["msg-1"], receiver.CompletedMessages.Select(m => m.MessageId));
            Assert.Equal(["msg-2", "msg-3"], receiver.AbandonedMessages.Select(m => m.MessageId));
        }
    }

    public class IngestForEventSubscription
    {
        [Fact]
        public async Task OnSuccess_CompletesEachMessage()
        {
            var repository = new FakeDeadLetterRepository();
            var receiver = new FakeDeadLetterReceiver();
            var ingestor = BuildIngestor(repository);
            var messages = new FakeDeadLetterMessage[] { new() { MessageId = "msg-1" }, new() { MessageId = "msg-2" } };

            await ingestor.IngestForEventSubscription(receiver, messages, "order-placed", "notification-handler", "order-placed/notification-handler", CancellationToken.None);

            Assert.Equal(2, receiver.CompletedMessages.Count);
            Assert.Empty(receiver.AbandonedMessages);
        }

        [Fact]
        public async Task OnSuccess_SavesBatchToRepositoryUnderTopicAndSubscriptionName()
        {
            var repository = new FakeDeadLetterRepository();
            var receiver = new FakeDeadLetterReceiver();
            var ingestor = BuildIngestor(repository);
            var messages = new FakeDeadLetterMessage[] { new() { MessageId = "msg-1" } };

            await ingestor.IngestForEventSubscription(receiver, messages, "order-placed", "notification-handler", "order-placed/notification-handler", CancellationToken.None);

            Assert.Equal("order-placed", repository.SavedQueueName);
            Assert.Equal("notification-handler", repository.SavedSubscriptionName);
            Assert.Single(repository.SavedMessages);
        }

        [Fact]
        public async Task WhenRepositoryThrows_AbandonsEachMessage()
        {
            var repository = new FakeDeadLetterRepository { ThrowOnSaveBatch = true };
            var receiver = new FakeDeadLetterReceiver();
            var ingestor = BuildIngestor(repository);
            var messages = new FakeDeadLetterMessage[] { new() { MessageId = "msg-1" }, new() { MessageId = "msg-2" } };

            await ingestor.IngestForEventSubscription(receiver, messages, "order-placed", "notification-handler", "order-placed/notification-handler", CancellationToken.None);

            Assert.Equal(2, receiver.AbandonedMessages.Count);
            Assert.Empty(receiver.CompletedMessages);
        }

        [Fact]
        public async Task WhenRepositoryThrows_DoesNotThrow()
        {
            var repository = new FakeDeadLetterRepository { ThrowOnSaveBatch = true };
            var receiver = new FakeDeadLetterReceiver();
            var ingestor = BuildIngestor(repository);

            await ingestor.IngestForEventSubscription(receiver, [new FakeDeadLetterMessage { MessageId = "msg-1" }], "order-placed", "notification-handler", "order-placed/notification-handler", CancellationToken.None);
        }
    }

    private static DeadLetterBatchIngestor BuildIngestor(FakeDeadLetterRepository repository)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IDeadLetterRepository>(repository);
        var scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

        return new DeadLetterBatchIngestor(scopeFactory, NullLogger<DeadLetterBatchIngestor>.Instance);
    }
}
