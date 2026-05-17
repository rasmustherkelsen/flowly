using System.Threading.Channels;
using Flowly.Transport;

namespace Flowly.InMemory.Tests;

public class InMemoryMessageBusSenderTests
{
    private static (InMemoryBroker broker, InMemoryMessageBusSender sender) CreateQueueSender(
        long? maxMessageSizeBytes = null)
    {
        var options = new InMemoryOptions { MaxMessageSizeBytes = maxMessageSizeBytes ?? 1_048_576 };
        var broker = new InMemoryBroker(options);
        var sender = new InMemoryMessageBusSender("test-queue", broker, options, SenderMode.Queue);
        return (broker, sender);
    }

    public class SendMessage
    {
        [Fact]
        public async Task WritesSerializedMessageToQueue()
        {
            var (broker, sender) = CreateQueueSender();

            await sender.SendMessage(new TestMessage("hello"), MessageProperties.Empty);

            Assert.True(broker.GetQueue("test-queue").Reader.TryRead(out var envelope));
            Assert.Contains("hello", envelope.RawBody);
        }

        [Fact]
        public async Task SetsMessageIdOnEnvelope()
        {
            var (broker, sender) = CreateQueueSender();
            var props = MessageProperties.Empty with { MessageId = "my-id" };

            await sender.SendMessage(new TestMessage("x"), props);

            broker.GetQueue("test-queue").Reader.TryRead(out var envelope);
            Assert.Equal("my-id", envelope!.MessageId);
        }

        [Fact]
        public async Task GeneratesMessageIdWhenNotProvided()
        {
            var (broker, sender) = CreateQueueSender();

            await sender.SendMessage(new TestMessage("x"), MessageProperties.Empty);

            broker.GetQueue("test-queue").Reader.TryRead(out var envelope);
            Assert.NotEmpty(envelope!.MessageId);
        }

        [Fact]
        public async Task SetsRetryCountInApplicationProperties()
        {
            var (broker, sender) = CreateQueueSender();
            var props = MessageProperties.Empty with { RetryCount = 2 };

            await sender.SendMessage(new TestMessage("x"), props);

            broker.GetQueue("test-queue").Reader.TryRead(out var envelope);
            Assert.Equal(2, envelope!.ApplicationProperties[FlowlyMessageProperties.RetryCount]);
        }

        [Fact]
        public async Task WithSessionId_WritesToSessionChannel()
        {
            var options = new InMemoryOptions();
            var broker = new InMemoryBroker(options);
            var sender = new InMemoryMessageBusSender("test-queue", broker, options, SenderMode.Queue);
            var props = MessageProperties.Empty with { SessionId = "lane-1" };

            await sender.SendMessage(new TestMessage("x"), props);

            Assert.True(broker.GetSessionChannel("test-queue", "lane-1").Reader.TryRead(out _));
            Assert.False(broker.GetQueue("test-queue").Reader.TryRead(out _));
        }

        [Fact]
        public async Task WithScheduledEnqueueTime_DoesNotWriteDirectlyToQueue()
        {
            var (broker, sender) = CreateQueueSender();
            var props = MessageProperties.Empty with { ScheduledEnqueueTime = DateTimeOffset.UtcNow.AddHours(1) };

            await sender.SendMessage(new TestMessage("x"), props);

            Assert.False(broker.GetQueue("test-queue").Reader.TryRead(out _));
        }

        [Fact]
        public async Task WithScheduledEnqueueTime_DeliversAfterDelay()
        {
            var (broker, sender) = CreateQueueSender();
            var props = MessageProperties.Empty with { ScheduledEnqueueTime = DateTimeOffset.UtcNow.AddSeconds(-1) };

            await sender.SendMessage(new TestMessage("x"), props);
            broker.DeliverReadyScheduledMessages();

            Assert.True(broker.GetQueue("test-queue").Reader.TryRead(out _));
        }

        [Fact]
        public async Task WhenMessageExceedsLimit_ThrowsMessageTooLargeException()
        {
            var (_, sender) = CreateQueueSender(maxMessageSizeBytes: 1);

            var exception = await Assert.ThrowsAsync<MessageTooLargeException>(
                () => sender.SendMessage(new TestMessage("hello"), MessageProperties.Empty));

            Assert.Equal("test-queue", exception.QueueName);
            Assert.Equal(1, exception.MaxSizeBytes);
            Assert.True(exception.ActualSizeBytes > 1);
        }
    }

    public class ReferencePassing
    {
        [Fact]
        public async Task StoresOriginalMessageReferenceInEnvelope()
        {
            var options = new InMemoryOptions { EnableReferencePassing = true };
            var broker = new InMemoryBroker(options);
            var sender = new InMemoryMessageBusSender("test-queue", broker, options, SenderMode.Queue);
            var message = new TestMessage("ref");

            await sender.SendMessage(message, MessageProperties.Empty);

            broker.GetQueue("test-queue").Reader.TryRead(out var envelope);
            Assert.Same(message, envelope!.OriginalMessage);
        }

        [Fact]
        public async Task RawBodyIsEmptyWhenReferencePassingEnabled()
        {
            var options = new InMemoryOptions { EnableReferencePassing = true };
            var broker = new InMemoryBroker(options);
            var sender = new InMemoryMessageBusSender("test-queue", broker, options, SenderMode.Queue);

            await sender.SendMessage(new TestMessage("ref"), MessageProperties.Empty);

            broker.GetQueue("test-queue").Reader.TryRead(out var envelope);
            Assert.Equal(string.Empty, envelope!.RawBody);
        }

        [Fact]
        public async Task MessageSizeIsNotValidatedWhenReferencePassingEnabled()
        {
            var options = new InMemoryOptions { EnableReferencePassing = true, MaxMessageSizeBytes = 1 };
            var broker = new InMemoryBroker(options);
            var sender = new InMemoryMessageBusSender("test-queue", broker, options, SenderMode.Queue);

            await sender.SendMessage(new TestMessage("this-would-exceed-1-byte"), MessageProperties.Empty);

            Assert.True(broker.GetQueue("test-queue").Reader.TryRead(out _));
        }
    }

    public class SendEmptyMessage
    {
        [Fact]
        public async Task WritesEnvelopeWithEmptyBody()
        {
            var (broker, sender) = CreateQueueSender();

            await sender.SendEmptyMessage(MessageProperties.Empty);

            Assert.True(broker.GetQueue("test-queue").Reader.TryRead(out var envelope));
            Assert.Equal(string.Empty, envelope.RawBody);
        }
    }

    public class SendRawMessage
    {
        [Fact]
        public async Task WritesRawBodyToQueue()
        {
            var (broker, sender) = CreateQueueSender();

            await sender.SendRawMessage("{\"x\":1}", new Dictionary<string, object>());

            Assert.True(broker.GetQueue("test-queue").Reader.TryRead(out var envelope));
            Assert.Equal("{\"x\":1}", envelope.RawBody);
        }

        [Fact]
        public async Task CopiesApplicationPropertiesOntoEnvelope()
        {
            var (broker, sender) = CreateQueueSender();
            var props = new Dictionary<string, object> { ["key"] = "value" };

            await sender.SendRawMessage("{}", props);

            broker.GetQueue("test-queue").Reader.TryRead(out var envelope);
            Assert.Equal("value", envelope!.ApplicationProperties["key"]);
        }

        [Fact]
        public async Task WhenMessageExceedsLimit_ThrowsMessageTooLargeException()
        {
            var (_, sender) = CreateQueueSender(maxMessageSizeBytes: 1);

            await Assert.ThrowsAsync<MessageTooLargeException>(
                () => sender.SendRawMessage("hello", new Dictionary<string, object>()));
        }
    }

    public class TopicFanOut
    {
        [Fact]
        public async Task PublishesToAllSubscriptions()
        {
            var options = new InMemoryOptions();
            var broker = new InMemoryBroker(options);
            broker.GetEventSubscriptionChannel("events", "handler-a");
            broker.GetEventSubscriptionChannel("events", "handler-b");

            var sender = new InMemoryMessageBusSender("events", broker, options, SenderMode.Topic);

            await sender.SendMessage(new TestMessage("x"), MessageProperties.Empty);

            Assert.True(broker.GetEventSubscriptionChannel("events", "handler-a").Reader.TryRead(out _));
            Assert.True(broker.GetEventSubscriptionChannel("events", "handler-b").Reader.TryRead(out _));
        }

        [Fact]
        public async Task WithTargetSubscription_PublishesOnlyToMatchingSubscription()
        {
            var options = new InMemoryOptions();
            var broker = new InMemoryBroker(options);
            broker.GetEventSubscriptionChannel("events", "handler-a");
            broker.GetEventSubscriptionChannel("events", "handler-b");

            var sender = new InMemoryMessageBusSender("events", broker, options, SenderMode.TopicRetry);
            var props = new Dictionary<string, object> { [FlowlyMessageProperties.TargetSubscription] = "handler-a" };

            await sender.SendRawMessage("{}", props);

            Assert.True(broker.GetEventSubscriptionChannel("events", "handler-a").Reader.TryRead(out _));
            Assert.False(broker.GetEventSubscriptionChannel("events", "handler-b").Reader.TryRead(out _));
        }
    }

    private record TestMessage(string Value);
}
