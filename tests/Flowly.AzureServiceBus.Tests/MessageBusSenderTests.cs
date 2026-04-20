using Azure.Messaging.ServiceBus;
using Flowly.MessagingAbstractions;

namespace Flowly.AzureServiceBus.Tests;

public class MessageBusSenderTests
{
    public class SendMessage
    {
        [Fact]
        public async Task WhenNoLimitConfigured_DoesNotThrow()
        {
            var sender = new MessageBusSender(new FakeServiceBusSender(), maxMessageSizeBytes: null);

            await sender.SendMessage(new TestMessage("hello"), MessageProperties.Empty);
        }

        [Fact]
        public async Task WhenMessageIsWithinLimit_DoesNotThrow()
        {
            var sender = new MessageBusSender(new FakeServiceBusSender(), maxMessageSizeBytes: 1024);

            await sender.SendMessage(new TestMessage("hello"), MessageProperties.Empty);
        }

        [Fact]
        public async Task WhenMessageExceedsLimit_ThrowsMessageTooLargeException()
        {
            var sender = new MessageBusSender(new FakeServiceBusSender(), maxMessageSizeBytes: 1);

            var exception = await Assert.ThrowsAsync<MessageTooLargeException>(
                () => sender.SendMessage(new TestMessage("hello"), MessageProperties.Empty));

            Assert.Equal(FakeServiceBusSender.QueueName, exception.QueueName);
            Assert.Equal(1, exception.MaxSizeBytes);
            Assert.True(exception.ActualSizeBytes > 1);
        }
    }

    public class SendRawMessage
    {
        [Fact]
        public async Task WhenNoLimitConfigured_DoesNotThrow()
        {
            var sender = new MessageBusSender(new FakeServiceBusSender(), maxMessageSizeBytes: null);

            await sender.SendRawMessage("hello", new Dictionary<string, object>());
        }

        [Fact]
        public async Task WhenMessageIsWithinLimit_DoesNotThrow()
        {
            var sender = new MessageBusSender(new FakeServiceBusSender(), maxMessageSizeBytes: 1024);

            await sender.SendRawMessage("hello", new Dictionary<string, object>());
        }

        [Fact]
        public async Task WhenMessageExceedsLimit_ThrowsMessageTooLargeException()
        {
            var sender = new MessageBusSender(new FakeServiceBusSender(), maxMessageSizeBytes: 1);

            var exception = await Assert.ThrowsAsync<MessageTooLargeException>(
                () => sender.SendRawMessage("hello", new Dictionary<string, object>()));

            Assert.Equal(FakeServiceBusSender.QueueName, exception.QueueName);
            Assert.Equal(1, exception.MaxSizeBytes);
            Assert.True(exception.ActualSizeBytes > 1);
        }
    }

    private record TestMessage(string Value);

    private class FakeServiceBusSender : ServiceBusSender
    {
        public const string QueueName = "test-queue";

        public override string EntityPath => QueueName;

        public override Task SendMessageAsync(ServiceBusMessage message, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
