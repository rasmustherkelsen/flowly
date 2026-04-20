using System.Text;
using System.Text.Json;
using Flowly.MessagingAbstractions;
using Flowly.RabbitMQ.Tests.Fakes;
using RabbitMQ.Client;

namespace Flowly.RabbitMQ.Tests;

public class RabbitMqEventPublisherTests
{
    public class SendMessage
    {
        [Fact]
        public async Task SerializesBodyAsJsonAndPublishesToExchange()
        {
            var recordingChannel = new RecordingChannel();
            var rabbitMqEventPublisher = new RabbitMqEventPublisher("orders-exchange", recordingChannel, maxMessageSizeBytes: null);

            await rabbitMqEventPublisher.SendMessage(new OrderPlaced("order-42"), MessageProperties.Empty);

            Assert.Equal("orders-exchange", recordingChannel.LastExchange);
            Assert.Equal("", recordingChannel.LastRoutingKey);
            var decodedBody = JsonSerializer.Deserialize<OrderPlaced>(recordingChannel.LastBody.Span);
            Assert.NotNull(decodedBody);
            Assert.Equal("order-42", decodedBody!.OrderId);
        }

        [Fact]
        public async Task WhenMessagePropertiesHasMessageId_PropagatesMessageId()
        {
            var recordingChannel = new RecordingChannel();
            var rabbitMqEventPublisher = new RabbitMqEventPublisher("orders-exchange", recordingChannel, maxMessageSizeBytes: null);

            await rabbitMqEventPublisher.SendMessage(
                new OrderPlaced("order-1"),
                new MessageProperties(MessageId: "my-message-id", CorrelationId: string.Empty));

            Assert.Equal("my-message-id", recordingChannel.LastBasicProperties!.MessageId);
        }

        [Fact]
        public async Task WhenMessageIdIsEmpty_GeneratesMessageId()
        {
            var recordingChannel = new RecordingChannel();
            var rabbitMqEventPublisher = new RabbitMqEventPublisher("orders-exchange", recordingChannel, maxMessageSizeBytes: null);

            await rabbitMqEventPublisher.SendMessage(new OrderPlaced("order-1"), MessageProperties.Empty);

            Assert.NotNull(recordingChannel.LastBasicProperties!.MessageId);
            Assert.NotEqual(string.Empty, recordingChannel.LastBasicProperties.MessageId);
            Assert.True(Guid.TryParse(recordingChannel.LastBasicProperties.MessageId, out _));
        }

        [Fact]
        public async Task WhenRetryCountIsPositive_AddsRetryCountHeader()
        {
            var recordingChannel = new RecordingChannel();
            var rabbitMqEventPublisher = new RabbitMqEventPublisher("orders-exchange", recordingChannel, maxMessageSizeBytes: null);

            await rabbitMqEventPublisher.SendMessage(
                new OrderPlaced("order-1"),
                new MessageProperties(MessageId: "m", CorrelationId: "", RetryCount: 3));

            var headers = recordingChannel.LastBasicProperties!.Headers!;
            Assert.True(headers.ContainsKey(FlowlyMessageProperties.RetryCount));
            Assert.Equal(3, headers[FlowlyMessageProperties.RetryCount]);
        }

        [Fact]
        public async Task WhenRetryCountIsZero_DoesNotAddRetryCountHeader()
        {
            var recordingChannel = new RecordingChannel();
            var rabbitMqEventPublisher = new RabbitMqEventPublisher("orders-exchange", recordingChannel, maxMessageSizeBytes: null);

            await rabbitMqEventPublisher.SendMessage(new OrderPlaced("order-1"), MessageProperties.Empty);

            var headers = recordingChannel.LastBasicProperties!.Headers!;
            Assert.False(headers.ContainsKey(FlowlyMessageProperties.RetryCount));
        }

        [Fact]
        public async Task WhenMessageExceedsLimit_ThrowsMessageTooLargeException()
        {
            var recordingChannel = new RecordingChannel();
            var rabbitMqEventPublisher = new RabbitMqEventPublisher("orders-exchange", recordingChannel, maxMessageSizeBytes: 1);

            var messageTooLargeException = await Assert.ThrowsAsync<MessageTooLargeException>(
                () => rabbitMqEventPublisher.SendMessage(new OrderPlaced("order-1"), MessageProperties.Empty));

            Assert.Equal("orders-exchange", messageTooLargeException.QueueName);
            Assert.Equal(1, messageTooLargeException.MaxSizeBytes);
        }
    }

    public class SendEmptyMessage
    {
        [Fact]
        public async Task PublishesEmptyBody()
        {
            var recordingChannel = new RecordingChannel();
            var rabbitMqEventPublisher = new RabbitMqEventPublisher("orders-exchange", recordingChannel, maxMessageSizeBytes: null);

            await rabbitMqEventPublisher.SendEmptyMessage(new MessageProperties(MessageId: "mid", CorrelationId: "cid"));

            Assert.Equal(0, recordingChannel.LastBody.Length);
            Assert.Equal("mid", recordingChannel.LastBasicProperties!.MessageId);
        }
    }

    public class SendRawMessage
    {
        [Fact]
        public async Task PublishesRawBytesAndPassesApplicationPropertiesAsHeaders()
        {
            var recordingChannel = new RecordingChannel();
            var rabbitMqEventPublisher = new RabbitMqEventPublisher("orders-exchange", recordingChannel, maxMessageSizeBytes: null);
            var applicationProperties = new Dictionary<string, object>
            {
                ["flowly-target-subscription"] = "warehouse",
                ["custom-header"] = "value"
            };

            await rabbitMqEventPublisher.SendRawMessage("raw-body", applicationProperties);

            Assert.Equal("raw-body", Encoding.UTF8.GetString(recordingChannel.LastBody.Span));
            var headers = recordingChannel.LastBasicProperties!.Headers!;
            Assert.Equal("warehouse", headers["flowly-target-subscription"]);
            Assert.Equal("value", headers["custom-header"]);
        }

        [Fact]
        public async Task WhenBodyExceedsLimit_ThrowsMessageTooLargeException()
        {
            var recordingChannel = new RecordingChannel();
            var rabbitMqEventPublisher = new RabbitMqEventPublisher("orders-exchange", recordingChannel, maxMessageSizeBytes: 2);
            var body = "hello-world-this-is-long";

            var messageTooLargeException = await Assert.ThrowsAsync<MessageTooLargeException>(
                () => rabbitMqEventPublisher.SendRawMessage(body, new Dictionary<string, object>()));

            Assert.Equal("orders-exchange", messageTooLargeException.QueueName);
            Assert.Equal(2, messageTooLargeException.MaxSizeBytes);
            Assert.True(messageTooLargeException.ActualSizeBytes > 2);
        }
    }

    private record OrderPlaced(string OrderId);

    private class RecordingChannel : ChannelStub
    {
        public string? LastExchange { get; private set; }
        public string? LastRoutingKey { get; private set; }
        public ReadOnlyMemory<byte> LastBody { get; private set; }
        public IReadOnlyBasicProperties? LastBasicProperties { get; private set; }

        public override ValueTask BasicPublishAsync<TProperties>(
            string exchange,
            string routingKey,
            bool mandatory,
            TProperties basicProperties,
            ReadOnlyMemory<byte> body,
            CancellationToken cancellationToken = default)
        {
            LastExchange = exchange;
            LastRoutingKey = routingKey;
            LastBody = body;
            LastBasicProperties = basicProperties;
            return ValueTask.CompletedTask;
        }
    }
}
