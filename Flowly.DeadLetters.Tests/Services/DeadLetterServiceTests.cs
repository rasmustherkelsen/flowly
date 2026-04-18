using Flowly.DeadLetters.BackgroundServices;
using Flowly.DeadLetters.DatabaseModel;
using Flowly.DeadLetters.Services;
using Flowly.DeadLetters.Tests.Fakes;
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
            repository.Add(BuildDeadLetter("msg-1", DeadLetterStatus.Pending, properties: """{"source":"test"}"""));
            var messageBusClient = new FakeMessageBusClient();
            var deadLetterService = BuildService(repository, messageBusClient);

            await deadLetterService.Requeue("msg-1");

            var sender = messageBusClient.GetSender("test-queue");
            var sent = sender.SentRawMessages[0];
            Assert.Equal("test", sent.ApplicationProperties["source"]);
        }

        [Fact]
        public async Task StripsRetryCount_FromSentMessage()
        {
            var repository = new FakeDeadLetterRepository();
            repository.Add(BuildDeadLetter("msg-1", DeadLetterStatus.Pending, properties: """{"flowly-retry-count":3,"source":"test"}"""));
            var messageBusClient = new FakeMessageBusClient();
            var deadLetterService = BuildService(repository, messageBusClient);

            await deadLetterService.Requeue("msg-1");

            var sent = messageBusClient.GetSender("test-queue").SentRawMessages[0];
            Assert.DoesNotContain(FlowlyMessageProperties.RetryCount, sent.ApplicationProperties.Keys);
            Assert.Equal("test", sent.ApplicationProperties["source"]);
        }

        [Fact]
        public async Task DoesNotIncludeTargetSubscriptionProperty_ForQueueDeadLetter()
        {
            var repository = new FakeDeadLetterRepository();
            repository.Add(BuildDeadLetter("msg-1", DeadLetterStatus.Pending));
            var messageBusClient = new FakeMessageBusClient();
            var deadLetterService = BuildService(repository, messageBusClient);

            await deadLetterService.Requeue("msg-1");

            var sender = messageBusClient.GetSender("test-queue");
            Assert.DoesNotContain(FlowlyMessageProperties.TargetSubscription, sender.SentRawMessages[0].ApplicationProperties.Keys);
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

    public class RequeueEventSubscriptionDeadLetter
    {
        [Fact]
        public async Task UsesEventRetrySender_WithTopicAndSubscriptionName()
        {
            var repository = new FakeDeadLetterRepository();
            repository.Add(BuildDeadLetter("msg-1", DeadLetterStatus.Pending, queueName: "order-placed", subscriptionName: "notification-handler", body: "hello"));
            var messageBusClient = new FakeMessageBusClient();
            var deadLetterService = BuildServiceWithEventSubscription(repository, messageBusClient, "order-placed", "notification-handler");

            await deadLetterService.Requeue("msg-1");

            Assert.True(messageBusClient.EventRetrySenderCreated("order-placed", "notification-handler"));
            var sender = messageBusClient.GetEventRetrySender("order-placed", "notification-handler");
            Assert.Single(sender.SentRawMessages);
            Assert.Equal("hello", sender.SentRawMessages[0].RawBody);
        }

        [Fact]
        public async Task DoesNotUseRegularMessageBusSender()
        {
            var repository = new FakeDeadLetterRepository();
            repository.Add(BuildDeadLetter("msg-1", DeadLetterStatus.Pending, queueName: "order-placed", subscriptionName: "notification-handler"));
            var messageBusClient = new FakeMessageBusClient();
            var deadLetterService = BuildServiceWithEventSubscription(repository, messageBusClient, "order-placed", "notification-handler");

            await deadLetterService.Requeue("msg-1");

            Assert.Empty(messageBusClient.CreatedSenders);
        }

        [Fact]
        public async Task MarksMessageAsRequeued()
        {
            var repository = new FakeDeadLetterRepository();
            repository.Add(BuildDeadLetter("msg-1", DeadLetterStatus.Pending, queueName: "order-placed", subscriptionName: "notification-handler"));
            var deadLetterService = BuildServiceWithEventSubscription(repository, new FakeMessageBusClient(), "order-placed", "notification-handler");

            await deadLetterService.Requeue("msg-1", requeuedBy: "admin");

            Assert.Equal("msg-1", repository.RequeuedMessageId);
            Assert.Equal("admin", repository.RequeuedBy);
        }

        [Fact]
        public async Task IncludesTargetSubscriptionProperty_InSentMessage()
        {
            var repository = new FakeDeadLetterRepository();
            repository.Add(BuildDeadLetter("msg-1", DeadLetterStatus.Pending, queueName: "order-placed", subscriptionName: "notification-handler"));
            var messageBusClient = new FakeMessageBusClient();
            var deadLetterService = BuildServiceWithEventSubscription(repository, messageBusClient, "order-placed", "notification-handler");

            await deadLetterService.Requeue("msg-1");

            var sender = messageBusClient.GetEventRetrySender("order-placed", "notification-handler");
            var sent = sender.SentRawMessages[0];
            Assert.Equal("notification-handler", sent.ApplicationProperties[FlowlyMessageProperties.TargetSubscription]);
        }
    }

    private static DeadLetterService BuildService(FakeDeadLetterRepository repository, FakeMessageBusClient client) =>
        new(repository, new FakeMessageBusClientRegistry(client), [new DeadLetterIngestionSettings("test-queue", "azure-service-bus")], []);

    private static DeadLetterService BuildServiceWithEventSubscription(
        FakeDeadLetterRepository repository,
        FakeMessageBusClient client,
        string topicName,
        string subscriptionName)
        => new(repository, new FakeMessageBusClientRegistry(client), [], [new EventSubscriptionDeadLetterIngestionSettings(topicName, subscriptionName, "azure-service-bus")]);

    private static DeadLetter BuildDeadLetter(
        string messageId,
        DeadLetterStatus status = DeadLetterStatus.Pending,
        string queueName = "test-queue",
        string? subscriptionName = null,
        string body = "{}",
        string properties = "{}")
        => new()
        {
            MessageId = messageId,
            QueueName = queueName,
            SubscriptionName = subscriptionName,
            MessageBody = body,
            MessageProperties = properties,
            DeadLetteredAt = DateTimeOffset.UtcNow,
            Status = status
        };
}
