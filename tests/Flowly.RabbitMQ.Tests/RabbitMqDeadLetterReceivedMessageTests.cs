using System.Text;
using Flowly.MessagingAbstractions;
using RabbitMQ.Client;

namespace Flowly.RabbitMQ.Tests;

public class RabbitMqDeadLetterReceivedMessageTests
{
    public class MessageId
    {
        [Fact]
        public void WithMessageId_ExtractsMessageId()
        {
            var props = new BasicProperties { MessageId = "dead-msg-1" };

            var message = new RabbitMqDeadLetterReceivedMessage(1, Encode("body"), props);

            Assert.Equal("dead-msg-1", message.MessageId);
        }

        [Fact]
        public void WithNoMessageId_UsesEmptyString()
        {
            var message = new RabbitMqDeadLetterReceivedMessage(1, Encode("body"), new BasicProperties());

            Assert.Equal(string.Empty, message.MessageId);
        }
    }

    public class RawBody
    {
        [Fact]
        public void DecodesBodyAsUtf8()
        {
            var message = new RabbitMqDeadLetterReceivedMessage(1, Encode("{\"key\":\"value\"}"), new BasicProperties());

            Assert.Equal("{\"key\":\"value\"}", message.RawBody);
        }
    }

    public class ApplicationProperties
    {
        [Fact]
        public void WithStringHeader_ExtractsValue()
        {
            var props = new BasicProperties
            {
                Headers = new Dictionary<string, object?> { ["app-key"] = "app-value" }
            };

            var message = new RabbitMqDeadLetterReceivedMessage(1, Encode("x"), props);

            Assert.Equal("app-value", message.ApplicationProperties["app-key"]);
        }

        [Fact]
        public void WithBytesHeader_DecodesUtf8()
        {
            var props = new BasicProperties
            {
                Headers = new Dictionary<string, object?> { ["app-key"] = Encoding.UTF8.GetBytes("decoded-value") }
            };

            var message = new RabbitMqDeadLetterReceivedMessage(1, Encode("x"), props);

            Assert.Equal("decoded-value", message.ApplicationProperties["app-key"]);
        }

        [Fact]
        public void XPrefixedHeaders_AreExcluded()
        {
            var props = new BasicProperties
            {
                Headers = new Dictionary<string, object?> { ["x-death"] = "internal", ["app-prop"] = "visible" }
            };

            var message = new RabbitMqDeadLetterReceivedMessage(1, Encode("x"), props);

            Assert.False(message.ApplicationProperties.ContainsKey("x-death"));
            Assert.True(message.ApplicationProperties.ContainsKey("app-prop"));
        }

        [Fact]
        public void WithNullHeaders_ApplicationPropertiesIsEmpty()
        {
            var message = new RabbitMqDeadLetterReceivedMessage(1, Encode("x"), new BasicProperties());

            Assert.Empty(message.ApplicationProperties);
        }
    }

    public class DeadLetterReason
    {
        [Fact]
        public void WithXDeathHeader_ExtractsReason()
        {
            var death = new Dictionary<string, object>
            {
                ["reason"] = Encoding.UTF8.GetBytes("rejected"),
                ["time"] = new AmqpTimestamp(1700000000)
            };
            var props = new BasicProperties
            {
                Headers = new Dictionary<string, object?> { ["x-death"] = new List<object> { death } }
            };

            var message = new RabbitMqDeadLetterReceivedMessage(1, Encode("x"), props);

            Assert.Equal("rejected", message.DeadLetterReason);
        }

        [Fact]
        public void WithNoXDeathHeader_ReasonIsNull()
        {
            var message = new RabbitMqDeadLetterReceivedMessage(1, Encode("x"), new BasicProperties());

            Assert.Null(message.DeadLetterReason);
        }

        [Fact]
        public void WithXDeathAmqpTimestamp_ExtractsEnqueuedTime()
        {
            var unixTime = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds();
            var death = new Dictionary<string, object>
            {
                ["reason"] = "expired",
                ["time"] = new AmqpTimestamp(unixTime)
            };
            var props = new BasicProperties
            {
                Headers = new Dictionary<string, object?> { ["x-death"] = new List<object> { death } }
            };

            var message = new RabbitMqDeadLetterReceivedMessage(1, Encode("x"), props);

            Assert.Equal(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero), message.EnqueuedTime);
        }
    }

    private static ReadOnlyMemory<byte> Encode(string text) => Encoding.UTF8.GetBytes(text);
}
