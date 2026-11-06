using Flowly.DeadLetters.BackgroundServices;
using Flowly.DeadLetters.DatabaseModel;
using Flowly.DeadLetters.Repositories;
using Flowly.DeadLetters.Services;
using Flowly.DeadLetters.Telemetry;
using Flowly.DeadLetters.Tests.Fakes;
using Flowly.DeadLetters.Tests.Helpers;
using Flowly.MessageInfrastructure.Registration;
using Flowly.Transport;
using Microsoft.EntityFrameworkCore;

namespace Flowly.DeadLetters.Tests.Services;

public class DeadLetterServiceTests
{
    private static DeadLetterService BuildService(IDeadLetterRepository repository, FakeMessageBusClient client)
        => new(
            repository,
            new FakeMessageBusClientRegistry(client),
            [new DeadLetterIngestionSettings("test-queue", "azure-service-bus")],
            [],
            new NullDeadLetterOperationInstrumentation());

    private static DeadLetterService BuildServiceWithEventSubscription(
        IDeadLetterRepository repository,
        FakeMessageBusClient client,
        string topicName,
        string subscriptionName)
    {
        return new DeadLetterService(repository, new FakeMessageBusClientRegistry(client), [], [new EventSubscriptionDeadLetterIngestionSettings(topicName, subscriptionName, "azure-service-bus")], new NullDeadLetterOperationInstrumentation());
    }

    private static async Task Seed(SqliteDbContextFactory factory, DeadLetter deadLetter)
    {
        await using var context = await factory.CreateDbContextAsync();
        context.DeadLetters.Add(deadLetter);
        await context.SaveChangesAsync();
    }

    private static DeadLetter BuildDeadLetter(
        string messageId,
        DeadLetterStatus status = DeadLetterStatus.Pending,
        string queueName = "test-queue",
        string? subscriptionName = null,
        string body = "{}",
        string properties = "{}")
    {
        return new DeadLetter
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
        public async Task ThrowsStatusConflict_WhenStatusIsAlreadyRequeued()
        {
            var repository = new FakeDeadLetterRepository();
            repository.Add(BuildDeadLetter("msg-1", DeadLetterStatus.Requeued));
            var deadLetterService = BuildService(repository, new FakeMessageBusClient());

            await Assert.ThrowsAsync<DeadLetterStatusConflictException>(() => deadLetterService.Requeue("msg-1"));
        }

        [Fact]
        public async Task SendsRawMessageToOriginalQueue()
        {
            var repository = new FakeDeadLetterRepository();
            repository.Add(BuildDeadLetter("msg-1", DeadLetterStatus.Pending, "my-queue", body: "hello"));
            var messageBusClient = new FakeMessageBusClient();
            var deadLetterService = BuildService(repository, messageBusClient);

            await deadLetterService.Requeue("msg-1");

            var sender = messageBusClient.GetSender("my-queue");
            Assert.Single(sender.SentRawMessages);
            Assert.Equal("hello", sender.SentRawMessages[0].RawBody);
        }

        [Fact]
        public async Task WithoutIngestionSettingsAndStreamOnlyProviderRegisteredFirst_SendsViaFirstQueueCapableProvider()
        {
            var repository = new FakeDeadLetterRepository();
            repository.Add(BuildDeadLetter("msg-1", DeadLetterStatus.Pending, "unmapped-queue"));
            var messageBusClient = new FakeMessageBusClient();
            var clientRegistry = new MessageBusClientRegistry();
            clientRegistry.Register("streams", new FakeStreamOnlyClient(), null);
            clientRegistry.Register("queues", messageBusClient, null);
            var deadLetterService = new DeadLetterService(repository, clientRegistry, [], [], new NullDeadLetterOperationInstrumentation());

            await deadLetterService.Requeue("msg-1");

            Assert.Contains("unmapped-queue", messageBusClient.CreatedSenders);
        }

        [Fact]
        public async Task SendsApplicationPropertiesWithMessage()
        {
            var repository = new FakeDeadLetterRepository();
            repository.Add(BuildDeadLetter("msg-1", properties: """{"source":"test"}"""));
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
            repository.Add(BuildDeadLetter("msg-1", properties: """{"flowly-retry-count":3,"source":"test"}"""));
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
            repository.Add(BuildDeadLetter("msg-1"));
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
            repository.Add(BuildDeadLetter("msg-1"));
            var deadLetterService = BuildService(repository, new FakeMessageBusClient());

            await deadLetterService.Requeue("msg-1", "admin");

            Assert.Equal("msg-1", repository.RequeuedMessageId);
            Assert.Equal("admin", repository.RequeuedBy);
        }

        [Fact]
        public async Task WhenTheClaimIsLost_ThrowsStatusConflictAndPublishesNothing()
        {
            var repository = new FakeDeadLetterRepository { MarkAsRequeuedResult = false };
            repository.Add(BuildDeadLetter("msg-1"));
            var messageBusClient = new FakeMessageBusClient();
            var deadLetterService = BuildService(repository, messageBusClient);

            await Assert.ThrowsAsync<DeadLetterStatusConflictException>(() => deadLetterService.Requeue("msg-1"));

            Assert.Empty(messageBusClient.CreatedSenders);
            Assert.Null(repository.RevertedMessageId);
        }

        [Fact]
        public async Task WhenThePublishFails_RevertsTheClaim()
        {
            var repository = new FakeDeadLetterRepository();
            repository.Add(BuildDeadLetter("msg-1"));
            var messageBusClient = new FakeMessageBusClient { FailureToThrowOnSendRawMessage = new InvalidOperationException("broker down") };
            var deadLetterService = BuildService(repository, messageBusClient);

            await Assert.ThrowsAsync<InvalidOperationException>(() => deadLetterService.Requeue("msg-1"));

            Assert.Equal("msg-1", repository.RevertedMessageId);
        }

        [Fact]
        public async Task WhenThePublishSucceeds_DoesNotRevertTheClaim()
        {
            var repository = new FakeDeadLetterRepository();
            repository.Add(BuildDeadLetter("msg-1"));
            var deadLetterService = BuildService(repository, new FakeMessageBusClient());

            await deadLetterService.Requeue("msg-1");

            Assert.Null(repository.RevertedMessageId);
        }

        [Fact]
        public async Task WhenCalledTwiceForTheSameDeadLetter_PublishesOnce()
        {
            using var factory = new SqliteDbContextFactory();
            await Seed(factory, BuildDeadLetter("msg-1"));
            var messageBusClient = new FakeMessageBusClient();
            var deadLetterService = BuildService(new DeadLetterRepository(factory), messageBusClient);

            await deadLetterService.Requeue("msg-1", "first");
            await Assert.ThrowsAsync<DeadLetterStatusConflictException>(() => deadLetterService.Requeue("msg-1", "second"));

            Assert.Single(messageBusClient.GetSender("test-queue").SentRawMessages);
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
        public async Task ThrowsStatusConflict_WhenStatusIsAlreadyRequeued()
        {
            var repository = new FakeDeadLetterRepository();
            repository.Add(BuildDeadLetter("msg-1", DeadLetterStatus.Requeued));
            var deadLetterService = BuildService(repository, new FakeMessageBusClient());

            await Assert.ThrowsAsync<DeadLetterStatusConflictException>(() => deadLetterService.Discard("msg-1"));
        }

        [Fact]
        public async Task DeletesRecord()
        {
            var repository = new FakeDeadLetterRepository();
            repository.Add(BuildDeadLetter("msg-1"));
            var deadLetterService = BuildService(repository, new FakeMessageBusClient());

            await deadLetterService.Discard("msg-1");

            Assert.Equal("msg-1", repository.DeletedMessageId);
        }

        [Fact]
        public async Task DoesNotSendAnyMessages()
        {
            var repository = new FakeDeadLetterRepository();
            repository.Add(BuildDeadLetter("msg-1"));
            var messageBusClient = new FakeMessageBusClient();
            var deadLetterService = BuildService(repository, messageBusClient);

            await deadLetterService.Discard("msg-1");

            Assert.Empty(messageBusClient.CreatedSenders);
        }

        [Fact]
        public async Task WhenTheRecordIsNoLongerPending_ThrowsStatusConflict()
        {
            var repository = new FakeDeadLetterRepository { DeletePendingResult = false };
            repository.Add(BuildDeadLetter("msg-1"));
            var deadLetterService = BuildService(repository, new FakeMessageBusClient());

            await Assert.ThrowsAsync<DeadLetterStatusConflictException>(() => deadLetterService.Discard("msg-1"));
        }

        [Fact]
        public async Task WhenRepositoryDeleteThrows_PropagatesException()
        {
            var repository = new FakeDeadLetterRepository { MessageIdToThrowOnDelete = "msg-1" };
            repository.Add(BuildDeadLetter("msg-1"));
            var deadLetterService = BuildService(repository, new FakeMessageBusClient());

            await Assert.ThrowsAsync<InvalidOperationException>(() => deadLetterService.Discard("msg-1"));
        }
    }

    public class RequeueEventSubscriptionDeadLetter
    {
        [Fact]
        public async Task UsesEventRetrySender_WithTopicAndSubscriptionName()
        {
            var repository = new FakeDeadLetterRepository();
            repository.Add(BuildDeadLetter("msg-1", DeadLetterStatus.Pending, "order-placed", "notification-handler", "hello"));
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
            repository.Add(BuildDeadLetter("msg-1", DeadLetterStatus.Pending, "order-placed", "notification-handler"));
            var messageBusClient = new FakeMessageBusClient();
            var deadLetterService = BuildServiceWithEventSubscription(repository, messageBusClient, "order-placed", "notification-handler");

            await deadLetterService.Requeue("msg-1");

            Assert.Empty(messageBusClient.CreatedSenders);
        }

        [Fact]
        public async Task MarksMessageAsRequeued()
        {
            var repository = new FakeDeadLetterRepository();
            repository.Add(BuildDeadLetter("msg-1", DeadLetterStatus.Pending, "order-placed", "notification-handler"));
            var deadLetterService = BuildServiceWithEventSubscription(repository, new FakeMessageBusClient(), "order-placed", "notification-handler");

            await deadLetterService.Requeue("msg-1", "admin");

            Assert.Equal("msg-1", repository.RequeuedMessageId);
            Assert.Equal("admin", repository.RequeuedBy);
        }

        [Fact]
        public async Task IncludesTargetSubscriptionProperty_InSentMessage()
        {
            var repository = new FakeDeadLetterRepository();
            repository.Add(BuildDeadLetter("msg-1", DeadLetterStatus.Pending, "order-placed", "notification-handler"));
            var messageBusClient = new FakeMessageBusClient();
            var deadLetterService = BuildServiceWithEventSubscription(repository, messageBusClient, "order-placed", "notification-handler");

            await deadLetterService.Requeue("msg-1");

            var sender = messageBusClient.GetEventRetrySender("order-placed", "notification-handler");
            var sent = sender.SentRawMessages[0];
            Assert.Equal("notification-handler", sent.ApplicationProperties[FlowlyMessageProperties.TargetSubscription]);
        }

        [Fact]
        public async Task WithSubscriptionOnNonEventCapableClient_ThrowsInvalidOperationException()
        {
            var repository = new FakeDeadLetterRepository();
            repository.Add(BuildDeadLetter("msg-1", DeadLetterStatus.Pending, "order-placed", "notification-handler"));
            var nonEventCapableClient = new NonEventCapableFakeMessageBusClient();
            var deadLetterService = new DeadLetterService(
                repository,
                new FakeMessageBusClientRegistry(nonEventCapableClient),
                [],
                [new EventSubscriptionDeadLetterIngestionSettings("order-placed", "notification-handler", "azure-service-bus")],
                new NullDeadLetterOperationInstrumentation());

            await Assert.ThrowsAsync<InvalidOperationException>(() => deadLetterService.Requeue("msg-1"));
        }
    }

    public class RequeueWithReplacementMessageBody
    {
        [Fact]
        public async Task SendsTheReplacementInsteadOfTheStoredBody()
        {
            using var factory = new SqliteDbContextFactory();
            await Seed(factory, BuildDeadLetter("msg-1", body: """{"amount":"abc"}"""));
            var messageBusClient = new FakeMessageBusClient();
            var deadLetterService = BuildService(new DeadLetterRepository(factory), messageBusClient);

            await deadLetterService.Requeue("msg-1", "ops", """{"amount":12}""");

            Assert.Equal("""{"amount":12}""", Assert.Single(messageBusClient.GetSender("test-queue").SentRawMessages).RawBody);
        }

        [Fact]
        public async Task KeepsTheStoredBodyAndRecordsTheReplacementAfterThePublish()
        {
            using var factory = new SqliteDbContextFactory();
            await Seed(factory, BuildDeadLetter("msg-1", body: """{"amount":"abc"}"""));
            var deadLetterService = BuildService(new DeadLetterRepository(factory), new FakeMessageBusClient());

            await deadLetterService.Requeue("msg-1", "ops", """{"amount":12}""");

            await using var context = await factory.CreateDbContextAsync();
            var deadLetter = await context.DeadLetters.SingleAsync();
            Assert.Equal("""{"amount":"abc"}""", deadLetter.MessageBody);
            Assert.Equal("""{"amount":12}""", deadLetter.ReplacementMessageBody);
            Assert.Equal(DeadLetterStatus.Requeued, deadLetter.Status);
            Assert.Equal("ops", deadLetter.RequeuedBy);
            Assert.NotNull(deadLetter.RequeuedAt);
        }

        [Fact]
        public async Task WithNullReplacement_SendsTheStoredBodyAndRecordsNoReplacement()
        {
            using var factory = new SqliteDbContextFactory();
            await Seed(factory, BuildDeadLetter("msg-1", body: """{"amount":"abc"}"""));
            var messageBusClient = new FakeMessageBusClient();
            var deadLetterService = BuildService(new DeadLetterRepository(factory), messageBusClient);

            await deadLetterService.Requeue("msg-1", "ops", null);

            Assert.Equal("""{"amount":"abc"}""", Assert.Single(messageBusClient.GetSender("test-queue").SentRawMessages).RawBody);
            await using var context = await factory.CreateDbContextAsync();
            Assert.Null((await context.DeadLetters.SingleAsync()).ReplacementMessageBody);
        }

        [Fact]
        public async Task RequeueWithoutReplacementOverload_RecordsNoReplacement()
        {
            using var factory = new SqliteDbContextFactory();
            await Seed(factory, BuildDeadLetter("msg-1"));
            var deadLetterService = BuildService(new DeadLetterRepository(factory), new FakeMessageBusClient());

            await deadLetterService.Requeue("msg-1", "ops");

            await using var context = await factory.CreateDbContextAsync();
            var deadLetter = await context.DeadLetters.SingleAsync();
            Assert.Equal(DeadLetterStatus.Requeued, deadLetter.Status);
            Assert.Null(deadLetter.ReplacementMessageBody);
        }

        [Fact]
        public async Task WhenThePublishFails_LeavesTheRecordPendingWithoutAReplacement()
        {
            using var factory = new SqliteDbContextFactory();
            await Seed(factory, BuildDeadLetter("msg-1"));
            var messageBusClient = new FakeMessageBusClient { FailureToThrowOnSendRawMessage = new InvalidOperationException("broker down") };
            var deadLetterService = BuildService(new DeadLetterRepository(factory), messageBusClient);

            await Assert.ThrowsAsync<InvalidOperationException>(() => deadLetterService.Requeue("msg-1", "ops", """{"amount":12}"""));

            await using var context = await factory.CreateDbContextAsync();
            var deadLetter = await context.DeadLetters.SingleAsync();
            Assert.Equal(DeadLetterStatus.Pending, deadLetter.Status);
            Assert.Null(deadLetter.ReplacementMessageBody);
            Assert.Null(deadLetter.RequeuedAt);
        }

        [Fact]
        public async Task KeepsThePropertiesOfTheStoredMessage()
        {
            using var factory = new SqliteDbContextFactory();
            await Seed(factory, BuildDeadLetter("msg-1", properties: """{"flowly-retry-count":3,"source":"test"}"""));
            var messageBusClient = new FakeMessageBusClient();
            var deadLetterService = BuildService(new DeadLetterRepository(factory), messageBusClient);

            await deadLetterService.Requeue("msg-1", null, """{"amount":12}""");

            var sent = Assert.Single(messageBusClient.GetSender("test-queue").SentRawMessages);
            Assert.Equal("test", sent.ApplicationProperties["source"]);
            Assert.DoesNotContain(FlowlyMessageProperties.RetryCount, sent.ApplicationProperties.Keys);
        }

        [Fact]
        public async Task WithAnEmptyReplacement_ThrowsAndPublishesNothing()
        {
            using var factory = new SqliteDbContextFactory();
            await Seed(factory, BuildDeadLetter("msg-1"));
            var messageBusClient = new FakeMessageBusClient();
            var deadLetterService = BuildService(new DeadLetterRepository(factory), messageBusClient);

            await Assert.ThrowsAsync<InvalidReplacementMessageBodyException>(() => deadLetterService.Requeue("msg-1", null, "  "));

            Assert.Empty(messageBusClient.CreatedSenders);
            await using var context = await factory.CreateDbContextAsync();
            Assert.Equal(DeadLetterStatus.Pending, (await context.DeadLetters.SingleAsync()).Status);
        }

        [Fact]
        public async Task WithNonJsonReplacementForAJsonBody_Throws()
        {
            using var factory = new SqliteDbContextFactory();
            await Seed(factory, BuildDeadLetter("msg-1", body: """{"amount":"abc"}"""));
            var deadLetterService = BuildService(new DeadLetterRepository(factory), new FakeMessageBusClient());

            await Assert.ThrowsAsync<InvalidReplacementMessageBodyException>(() => deadLetterService.Requeue("msg-1", null, "{amount:"));
        }

        [Fact]
        public async Task WithNonJsonReplacementForANonJsonBody_Succeeds()
        {
            using var factory = new SqliteDbContextFactory();
            await Seed(factory, BuildDeadLetter("msg-1", body: "plain text"));
            var messageBusClient = new FakeMessageBusClient();
            var deadLetterService = BuildService(new DeadLetterRepository(factory), messageBusClient);

            await deadLetterService.Requeue("msg-1", null, "corrected text");

            Assert.Equal("corrected text", Assert.Single(messageBusClient.GetSender("test-queue").SentRawMessages).RawBody);
        }

        [Fact]
        public async Task WithMissingMessage_ThrowsKeyNotFoundBeforeValidating()
        {
            using var factory = new SqliteDbContextFactory();
            var deadLetterService = BuildService(new DeadLetterRepository(factory), new FakeMessageBusClient());

            await Assert.ThrowsAsync<KeyNotFoundException>(() => deadLetterService.Requeue("missing", null, ""));
        }

        [Fact]
        public async Task WithAlreadyRequeuedMessage_ThrowsStatusConflictBeforeValidating()
        {
            using var factory = new SqliteDbContextFactory();
            await Seed(factory, BuildDeadLetter("msg-1", DeadLetterStatus.Requeued));
            var deadLetterService = BuildService(new DeadLetterRepository(factory), new FakeMessageBusClient());

            await Assert.ThrowsAsync<DeadLetterStatusConflictException>(() => deadLetterService.Requeue("msg-1", null, ""));
        }

        [Fact]
        public async Task ForEventSubscriptionDeadLetter_SendsTheReplacementToTheSubscriberOnly()
        {
            using var factory = new SqliteDbContextFactory();
            await Seed(factory, BuildDeadLetter("msg-1", queueName: "order-placed", subscriptionName: "notification-handler", body: """{"order":"abc"}"""));
            var messageBusClient = new FakeMessageBusClient();
            var deadLetterService = BuildServiceWithEventSubscription(new DeadLetterRepository(factory), messageBusClient, "order-placed", "notification-handler");

            await deadLetterService.Requeue("msg-1", "ops", """{"order":"1"}""");

            var sent = Assert.Single(messageBusClient.GetEventRetrySender("order-placed", "notification-handler").SentRawMessages);
            Assert.Equal("""{"order":"1"}""", sent.RawBody);
            Assert.Equal("notification-handler", sent.ApplicationProperties[FlowlyMessageProperties.TargetSubscription]);
            await using var context = await factory.CreateDbContextAsync();
            Assert.Equal("""{"order":"1"}""", (await context.DeadLetters.SingleAsync()).ReplacementMessageBody);
        }
    }

    public class GetDeadLetters
    {
        [Fact]
        public async Task WithEmptyRepository_ReturnsEmptyCollection()
        {
            var repository = new FakeDeadLetterRepository();
            var deadLetterService = BuildService(repository, new FakeMessageBusClient());

            var result = await deadLetterService.GetDeadLetters();

            Assert.Empty(result);
        }

        [Fact]
        public async Task WithMultipleRecords_ReturnsAllDeadLetters()
        {
            var repository = new FakeDeadLetterRepository();
            repository.Add(BuildDeadLetter("msg-1"));
            repository.Add(BuildDeadLetter("msg-2"));
            repository.Add(BuildDeadLetter("msg-3"));
            var deadLetterService = BuildService(repository, new FakeMessageBusClient());

            var result = await deadLetterService.GetDeadLetters();

            Assert.Equal(3, result.Count);
        }

        [Fact]
        public async Task DelegatesToRepository()
        {
            var repository = new FakeDeadLetterRepository();
            var deadLetter = BuildDeadLetter("msg-1", queueName: "orders");
            repository.Add(deadLetter);
            var deadLetterService = BuildService(repository, new FakeMessageBusClient());

            var result = await deadLetterService.GetDeadLetters();

            Assert.Contains(result, d => d.MessageId == "msg-1" && d.QueueName == "orders");
        }
    }

    public class QueryDeadLetters
    {
        [Fact]
        public async Task DelegatesTheQueryToTheRepositoryAndReturnsItsPage()
        {
            var repository = new FakeDeadLetterRepository();
            var expectedPage = new DeadLetterPage([BuildDeadLetter("msg-1")], 7);
            repository.PageToReturn = expectedPage;
            var deadLetterService = BuildService(repository, new FakeMessageBusClient());
            var deadLetterQuery = new DeadLetterQuery { QueueName = "test-queue", Skip = 2, Take = 3, IncludeTotalCount = true };

            var result = await deadLetterService.QueryDeadLetters(deadLetterQuery);

            Assert.Same(expectedPage, result);
            Assert.Same(deadLetterQuery, repository.QueriedWith);
        }
    }

    public class RequeueJsonPropertyHandling
    {
        [Fact]
        public async Task PreservesStringProperties()
        {
            var repository = new FakeDeadLetterRepository();
            repository.Add(BuildDeadLetter("msg-1", properties: """{"source":"orders-api"}"""));
            var messageBusClient = new FakeMessageBusClient();
            var deadLetterService = BuildService(repository, messageBusClient);

            await deadLetterService.Requeue("msg-1");

            var sent = messageBusClient.GetSender("test-queue").SentRawMessages[0];
            Assert.Equal("orders-api", sent.ApplicationProperties["source"]);
        }

        [Fact]
        public async Task PreservesIntegerProperties()
        {
            var repository = new FakeDeadLetterRepository();
            repository.Add(BuildDeadLetter("msg-1", properties: """{"priority":42}"""));
            var messageBusClient = new FakeMessageBusClient();
            var deadLetterService = BuildService(repository, messageBusClient);

            await deadLetterService.Requeue("msg-1");

            var sent = messageBusClient.GetSender("test-queue").SentRawMessages[0];
            Assert.Equal(42, sent.ApplicationProperties["priority"]);
        }

        [Fact]
        public async Task PreservesBooleanTrueProperty()
        {
            var repository = new FakeDeadLetterRepository();
            repository.Add(BuildDeadLetter("msg-1", properties: """{"is-urgent":true}"""));
            var messageBusClient = new FakeMessageBusClient();
            var deadLetterService = BuildService(repository, messageBusClient);

            await deadLetterService.Requeue("msg-1");

            var sent = messageBusClient.GetSender("test-queue").SentRawMessages[0];
            Assert.Equal(true, sent.ApplicationProperties["is-urgent"]);
        }

        [Fact]
        public async Task PreservesBooleanFalseProperty()
        {
            var repository = new FakeDeadLetterRepository();
            repository.Add(BuildDeadLetter("msg-1", properties: """{"is-urgent":false}"""));
            var messageBusClient = new FakeMessageBusClient();
            var deadLetterService = BuildService(repository, messageBusClient);

            await deadLetterService.Requeue("msg-1");

            var sent = messageBusClient.GetSender("test-queue").SentRawMessages[0];
            Assert.Equal(false, sent.ApplicationProperties["is-urgent"]);
        }

        [Fact]
        public async Task WithEmptyPropertiesJson_SendsNoExtraProperties()
        {
            var repository = new FakeDeadLetterRepository();
            repository.Add(BuildDeadLetter("msg-1"));
            var messageBusClient = new FakeMessageBusClient();
            var deadLetterService = BuildService(repository, messageBusClient);

            await deadLetterService.Requeue("msg-1");

            var sent = messageBusClient.GetSender("test-queue").SentRawMessages[0];
            Assert.Empty(sent.ApplicationProperties);
        }
    }

    private sealed class NonEventCapableFakeMessageBusClient : IMessageBusClient
    {
        public string MessagingSystem => "fake-non-event";

        public Task<IMessageBusReceiver> CreateReceiver(string queueName) => throw new NotImplementedException();

        public Task<IMessageBusProcessor<TMessage>> CreateProcessor<TMessage>(string queueName, MessageBusProcessorOptions options)
            => throw new NotImplementedException();

        public Task<IExecutionLaneProcessor> CreateExecutionLaneProcessor(string queueName, string laneFilter, MessageBusProcessorOptions options)
            => throw new NotImplementedException();

        public Task<IMessageBusSender> CreateMessageBusSender(string queueName) => throw new NotImplementedException();

        public Task<IDeadLetterReceiver> CreateDeadLetterReceiver(string queueName) => throw new NotImplementedException();

        public Task<long> GetDeadLetterMessageCount(string queueName, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }
}