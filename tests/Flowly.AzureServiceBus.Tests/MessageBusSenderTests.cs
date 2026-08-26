using Azure.Messaging.ServiceBus;
using Flowly.Transport;

namespace Flowly.AzureServiceBus.Tests;

public class MessageBusSenderTests
{
    public class SendMessage
    {
        [Fact]
        public async Task WhenNoLimitConfigured_DoesNotThrow()
        {
            var sender = new MessageBusSender(new FakeServiceBusSender(), null);

            await sender.SendMessage(new TestMessage("hello"), MessageProperties.Empty);
        }

        [Fact]
        public async Task WhenMessageIsWithinLimit_DoesNotThrow()
        {
            var sender = new MessageBusSender(new FakeServiceBusSender(), 1024);

            await sender.SendMessage(new TestMessage("hello"), MessageProperties.Empty);
        }

        [Fact]
        public async Task WhenMessageExceedsLimit_ThrowsMessageTooLargeException()
        {
            var sender = new MessageBusSender(new FakeServiceBusSender(), 1);

            var exception = await Assert.ThrowsAsync<MessageTooLargeException>(() => sender.SendMessage(new TestMessage("hello"), MessageProperties.Empty));

            Assert.Equal(FakeServiceBusSender.QueueName, exception.QueueName);
            Assert.Equal(1, exception.MaxSizeBytes);
            Assert.True(exception.ActualSizeBytes > 1);
        }

        [Fact]
        public async Task WithoutExtraApplicationProperties_DoesNotSetAnyExtraProperties()
        {
            var serviceBusSender = new FakeServiceBusSender();
            var sender = new MessageBusSender(serviceBusSender, null);

            await sender.SendMessage(new TestMessage("hello"), MessageProperties.Empty);

            Assert.False(serviceBusSender.LastSentMessage!.ApplicationProperties.ContainsKey(FlowlyMessageProperties.TargetSubscription));
        }

        [Fact]
        public async Task WithExtraApplicationProperties_SetsThemOnTheOutgoingMessage()
        {
            var serviceBusSender = new FakeServiceBusSender();
            var sender = new MessageBusSender(serviceBusSender, null, new Dictionary<string, object> { [FlowlyMessageProperties.TargetSubscription] = "my-subscription" });

            await sender.SendMessage(new TestMessage("hello"), MessageProperties.Empty);

            Assert.Equal("my-subscription", serviceBusSender.LastSentMessage!.ApplicationProperties[FlowlyMessageProperties.TargetSubscription]);
        }
    }

    public class SendRawMessage
    {
        [Fact]
        public async Task WhenNoLimitConfigured_DoesNotThrow()
        {
            var sender = new MessageBusSender(new FakeServiceBusSender(), null);

            await sender.SendRawMessage("hello", new Dictionary<string, object>());
        }

        [Fact]
        public async Task WhenMessageIsWithinLimit_DoesNotThrow()
        {
            var sender = new MessageBusSender(new FakeServiceBusSender(), 1024);

            await sender.SendRawMessage("hello", new Dictionary<string, object>());
        }

        [Fact]
        public async Task WhenMessageExceedsLimit_ThrowsMessageTooLargeException()
        {
            var sender = new MessageBusSender(new FakeServiceBusSender(), 1);

            var exception = await Assert.ThrowsAsync<MessageTooLargeException>(() => sender.SendRawMessage("hello", new Dictionary<string, object>()));

            Assert.Equal(FakeServiceBusSender.QueueName, exception.QueueName);
            Assert.Equal(1, exception.MaxSizeBytes);
            Assert.True(exception.ActualSizeBytes > 1);
        }
    }

    public class DisposeAsync
    {
        [Fact]
        public async Task DisposesTheWrappedServiceBusSender()
        {
            var serviceBusSender = new FakeServiceBusSender();
            var sender = new MessageBusSender(serviceBusSender, null);

            await sender.DisposeAsync();

            Assert.True(serviceBusSender.DisposeAsyncCalled);
        }
    }

    private record TestMessage(string Value);

    private class FakeServiceBusSender : ServiceBusSender
    {
        public const string QueueName = "test-queue";

        public ServiceBusMessage? LastSentMessage { get; private set; }

        public bool DisposeAsyncCalled { get; private set; }

        public override string EntityPath => QueueName;

        public override Task SendMessageAsync(ServiceBusMessage message, CancellationToken cancellationToken = default)
        {
            LastSentMessage = message;
            return Task.CompletedTask;
        }

        public override ValueTask DisposeAsync()
        {
            DisposeAsyncCalled = true;
            return ValueTask.CompletedTask;
        }
    }
}