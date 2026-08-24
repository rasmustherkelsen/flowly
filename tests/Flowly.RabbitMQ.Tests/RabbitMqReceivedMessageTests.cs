using System.Text;
using System.Text.Json;
using Flowly.RabbitMQ.Tests.Fakes;
using Flowly.Transport;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Flowly.RabbitMQ.Tests;

public class RabbitMqReceivedMessageTests
{
    private static BasicDeliverEventArgs BuildArgs(TestMessage body, BasicProperties props, ulong deliveryTag = 1)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(body);

        return new BasicDeliverEventArgs(
            "consumer",
            deliveryTag,
            false,
            "",
            "queue",
            props,
            json);
    }

    public class Body
    {
        [Fact]
        public void WithValidJson_DeserializesBody()
        {
            var args = BuildArgs(new TestMessage("hello"), new BasicProperties());

            var message = new RabbitMqReceivedMessage<TestMessage>(new ChannelStub(), args);

            Assert.Equal("hello", message.Body.Value);
        }

        [Fact]
        public void BodyIsLazilyDeserialized()
        {
            var args = BuildArgs(new TestMessage("lazy"), new BasicProperties());
            var message = new RabbitMqReceivedMessage<TestMessage>(new ChannelStub(), args);

            var first = message.Body;
            var second = message.Body;

            Assert.Same(first, second);
        }

        [Fact]
        public void WhenUnderlyingBufferIsMutatedAfterConstruction_StillDeserializesOriginalBytes()
        {
            var originalBytes = JsonSerializer.SerializeToUtf8Bytes(new TestMessage("original"));
            var args = new BasicDeliverEventArgs("consumer", 1, false, "", "queue", new BasicProperties(), originalBytes);
            var message = new RabbitMqReceivedMessage<TestMessage>(new ChannelStub(), args);

            Array.Clear(originalBytes);

            Assert.Equal("original", message.Body.Value);
        }
    }

    public class Properties
    {
        [Fact]
        public void WithMessageIdAndCorrelationId_ExtractsBoth()
        {
            var props = new BasicProperties { MessageId = "msg-1", CorrelationId = "corr-1" };
            var message = new RabbitMqReceivedMessage<TestMessage>(new ChannelStub(), BuildArgs(new TestMessage("x"), props));

            Assert.Equal("msg-1", message.Properties.MessageId);
            Assert.Equal("corr-1", message.Properties.CorrelationId);
        }

        [Fact]
        public void WithNoHeaders_RetryCountIsZero()
        {
            var message = new RabbitMqReceivedMessage<TestMessage>(new ChannelStub(), BuildArgs(new TestMessage("x"), new BasicProperties()));

            Assert.Equal(0, message.Properties.RetryCount);
        }

        [Fact]
        public void WithRetryCountHeaderAsInt_ExtractsRetryCount()
        {
            var props = new BasicProperties
            {
                Headers = new Dictionary<string, object?> { [FlowlyMessageProperties.RetryCount] = 5 }
            };
            var message = new RabbitMqReceivedMessage<TestMessage>(new ChannelStub(), BuildArgs(new TestMessage("x"), props));

            Assert.Equal(5, message.Properties.RetryCount);
        }

        [Fact]
        public void WithRetryCountHeaderAsBytes_ExtractsRetryCount()
        {
            var props = new BasicProperties
            {
                Headers = new Dictionary<string, object?> { [FlowlyMessageProperties.RetryCount] = Encoding.UTF8.GetBytes("3") }
            };
            var message = new RabbitMqReceivedMessage<TestMessage>(new ChannelStub(), BuildArgs(new TestMessage("x"), props));

            Assert.Equal(3, message.Properties.RetryCount);
        }

        [Fact]
        public void WithTraceparentHeader_ExtractsTraceparent()
        {
            var props = new BasicProperties
            {
                Headers = new Dictionary<string, object?> { ["traceparent"] = Encoding.UTF8.GetBytes("00-trace-span-01") }
            };
            var message = new RabbitMqReceivedMessage<TestMessage>(new ChannelStub(), BuildArgs(new TestMessage("x"), props));

            Assert.Equal("00-trace-span-01", message.Properties.Traceparent);
        }

        [Fact]
        public void WithTracestateHeader_ExtractsTracestate()
        {
            var props = new BasicProperties
            {
                Headers = new Dictionary<string, object?> { ["tracestate"] = Encoding.UTF8.GetBytes("k=v") }
            };
            var message = new RabbitMqReceivedMessage<TestMessage>(new ChannelStub(), BuildArgs(new TestMessage("x"), props));

            Assert.Equal("k=v", message.Properties.Tracestate);
        }

        [Fact]
        public void WithNoHeaders_StreamOffsetIsNull()
        {
            var message = new RabbitMqReceivedMessage<TestMessage>(new ChannelStub(), BuildArgs(new TestMessage("x"), new BasicProperties()));

            Assert.Null(message.Properties.StreamOffset);
        }

        [Fact]
        public void WithStreamOffsetHeaderAsLong_ExtractsStreamOffset()
        {
            var props = new BasicProperties
            {
                Headers = new Dictionary<string, object?> { ["x-stream-offset"] = 42L }
            };
            var message = new RabbitMqReceivedMessage<TestMessage>(new ChannelStub(), BuildArgs(new TestMessage("x"), props));

            Assert.Equal(42L, message.Properties.StreamOffset);
        }

        [Fact]
        public void WithStreamOffsetHeaderAsBytes_ExtractsStreamOffset()
        {
            var props = new BasicProperties
            {
                Headers = new Dictionary<string, object?> { ["x-stream-offset"] = Encoding.UTF8.GetBytes("99") }
            };
            var message = new RabbitMqReceivedMessage<TestMessage>(new ChannelStub(), BuildArgs(new TestMessage("x"), props));

            Assert.Equal(99L, message.Properties.StreamOffset);
        }
    }

    public class Complete
    {
        [Fact]
        public async Task WhenNotAutoAck_AcksChannel()
        {
            var channel = new TrackingChannelStub();
            var message = new RabbitMqReceivedMessage<TestMessage>(channel, BuildArgs(new TestMessage("x"), new BasicProperties(), 42), false);

            await message.Complete();

            Assert.Equal(42UL, channel.AckedDeliveryTag);
        }

        [Fact]
        public async Task WhenAutoAck_DoesNotAckChannel()
        {
            var channel = new TrackingChannelStub();
            var message = new RabbitMqReceivedMessage<TestMessage>(channel, BuildArgs(new TestMessage("x"), new BasicProperties()), true);

            await message.Complete();

            Assert.Null(channel.AckedDeliveryTag);
        }
    }

    public class DeadLetter
    {
        [Fact]
        public async Task WhenNotAutoAck_NacksChannel()
        {
            var channel = new TrackingChannelStub();
            var message = new RabbitMqReceivedMessage<TestMessage>(channel, BuildArgs(new TestMessage("x"), new BasicProperties(), 7), false);

            await message.DeadLetter();

            Assert.Equal(7UL, channel.NackedDeliveryTag);
        }

        [Fact]
        public async Task WhenAutoAck_DoesNotNackChannel()
        {
            var channel = new TrackingChannelStub();
            var message = new RabbitMqReceivedMessage<TestMessage>(channel, BuildArgs(new TestMessage("x"), new BasicProperties()), true);

            await message.DeadLetter();

            Assert.Null(channel.NackedDeliveryTag);
        }
    }

    private record TestMessage(string Value);

    private class TrackingChannelStub : ChannelStub
    {
        public ulong? AckedDeliveryTag { get; private set; }
        public ulong? NackedDeliveryTag { get; private set; }

        public override ValueTask BasicAckAsync(ulong deliveryTag, bool multiple, CancellationToken cancellationToken = default)
        {
            AckedDeliveryTag = deliveryTag;

            return ValueTask.CompletedTask;
        }

        public override ValueTask BasicNackAsync(ulong deliveryTag, bool multiple, bool requeue, CancellationToken cancellationToken = default)
        {
            NackedDeliveryTag = deliveryTag;

            return ValueTask.CompletedTask;
        }
    }
}