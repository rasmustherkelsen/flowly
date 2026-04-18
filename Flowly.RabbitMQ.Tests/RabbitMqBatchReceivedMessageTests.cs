using System.Text;
using Flowly.MessagingAbstractions;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Flowly.RabbitMQ.Tests;

public class RabbitMqBatchReceivedMessageTests
{
    public class Properties
    {
        [Fact]
        public void WithNoHeaders_RetryCountIsZero()
        {
            var message = new RabbitMqBatchReceivedMessage<TestMessage>(
                new FakeChannel(),
                deliveryTag: 1,
                body: SerializeBody(new TestMessage("x")),
                properties: new BasicProperties());

            Assert.Equal(0, message.Properties.RetryCount);
        }

        [Fact]
        public void WithRetryCountHeader_ExtractsRetryCount()
        {
            var basicProperties = new BasicProperties
            {
                Headers = new Dictionary<string, object?> { [FlowlyMessageProperties.RetryCount] = 3 }
            };

            var message = new RabbitMqBatchReceivedMessage<TestMessage>(
                new FakeChannel(),
                deliveryTag: 1,
                body: SerializeBody(new TestMessage("x")),
                properties: basicProperties);

            Assert.Equal(3, message.Properties.RetryCount);
        }

        [Fact]
        public void WithRetryCountHeaderAsBytes_ExtractsRetryCount()
        {
            var basicProperties = new BasicProperties
            {
                Headers = new Dictionary<string, object?> { [FlowlyMessageProperties.RetryCount] = Encoding.UTF8.GetBytes("2") }
            };

            var message = new RabbitMqBatchReceivedMessage<TestMessage>(
                new FakeChannel(),
                deliveryTag: 1,
                body: SerializeBody(new TestMessage("x")),
                properties: basicProperties);

            Assert.Equal(2, message.Properties.RetryCount);
        }

        [Fact]
        public void WithTraceparentHeader_ExtractsTraceparent()
        {
            var basicProperties = new BasicProperties
            {
                Headers = new Dictionary<string, object?> { ["traceparent"] = Encoding.UTF8.GetBytes("00-abc-def-01") }
            };

            var message = new RabbitMqBatchReceivedMessage<TestMessage>(
                new FakeChannel(),
                deliveryTag: 1,
                body: SerializeBody(new TestMessage("x")),
                properties: basicProperties);

            Assert.Equal("00-abc-def-01", message.Properties.Traceparent);
        }

        [Fact]
        public void WithTracestateHeader_ExtractsTracestate()
        {
            var basicProperties = new BasicProperties
            {
                Headers = new Dictionary<string, object?> { ["tracestate"] = Encoding.UTF8.GetBytes("vendor=value") }
            };

            var message = new RabbitMqBatchReceivedMessage<TestMessage>(
                new FakeChannel(),
                deliveryTag: 1,
                body: SerializeBody(new TestMessage("x")),
                properties: basicProperties);

            Assert.Equal("vendor=value", message.Properties.Tracestate);
        }

        [Fact]
        public void WithNoTraceHeaders_TraceparentAndTracestateAreNull()
        {
            var message = new RabbitMqBatchReceivedMessage<TestMessage>(
                new FakeChannel(),
                deliveryTag: 1,
                body: SerializeBody(new TestMessage("x")),
                properties: new BasicProperties());

            Assert.Null(message.Properties.Traceparent);
            Assert.Null(message.Properties.Tracestate);
        }
    }

    private static ReadOnlyMemory<byte> SerializeBody(TestMessage message)
        => System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(message);

    private record TestMessage(string Value);

    private class FakeChannel : IChannel
    {
        public int ChannelNumber => 1;
        public ShutdownEventArgs? CloseReason => null;
        public TimeSpan ContinuationTimeout { get; set; } = TimeSpan.FromSeconds(10);
        public string? CurrentQueue => null;
        public bool IsClosed => false;
        public bool IsOpen => true;
        public IAsyncBasicConsumer? DefaultConsumer { get; set; }

        public event AsyncEventHandler<BasicAckEventArgs>? BasicAcksAsync { add { } remove { } }
        public event AsyncEventHandler<BasicNackEventArgs>? BasicNacksAsync { add { } remove { } }
        public event AsyncEventHandler<BasicReturnEventArgs>? BasicReturnAsync { add { } remove { } }
        public event AsyncEventHandler<CallbackExceptionEventArgs>? CallbackExceptionAsync { add { } remove { } }
        public event AsyncEventHandler<ShutdownEventArgs>? ChannelShutdownAsync { add { } remove { } }
        public event AsyncEventHandler<FlowControlEventArgs>? FlowControlAsync { add { } remove { } }

        public ValueTask BasicAckAsync(ulong deliveryTag, bool multiple, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask BasicNackAsync(ulong deliveryTag, bool multiple, bool requeue, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask BasicRejectAsync(ulong deliveryTag, bool requeue, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public Task BasicQosAsync(uint prefetchSize, ushort prefetchCount, bool global, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<string> BasicConsumeAsync(string queue, bool autoAck, string consumerTag, bool noLocal, bool exclusive, IDictionary<string, object?>? arguments, IAsyncBasicConsumer consumer, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task BasicCancelAsync(string consumerTag, bool noWait = false, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<BasicGetResult?> BasicGetAsync(string queue, bool autoAck, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public ValueTask BasicPublishAsync<TProperties>(string exchange, string routingKey, bool mandatory, TProperties basicProperties, ReadOnlyMemory<byte> body, CancellationToken cancellationToken = default) where TProperties : IReadOnlyBasicProperties, IAmqpHeader => throw new NotImplementedException();
        public ValueTask BasicPublishAsync<TProperties>(CachedString exchange, CachedString routingKey, bool mandatory, TProperties basicProperties, ReadOnlyMemory<byte> body, CancellationToken cancellationToken = default) where TProperties : IReadOnlyBasicProperties, IAmqpHeader => throw new NotImplementedException();
        public ValueTask<ulong> GetNextPublishSequenceNumberAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<uint> ConsumerCountAsync(string queue, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<uint> MessageCountAsync(string queue, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task ExchangeDeclareAsync(string exchange, string type, bool durable, bool autoDelete, IDictionary<string, object?>? arguments = null, bool passive = false, bool noWait = false, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task ExchangeDeclarePassiveAsync(string exchange, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task ExchangeDeleteAsync(string exchange, bool ifUnused, bool noWait = false, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task ExchangeBindAsync(string destination, string source, string routingKey, IDictionary<string, object?>? arguments = null, bool noWait = false, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task ExchangeUnbindAsync(string destination, string source, string routingKey, IDictionary<string, object?>? arguments = null, bool noWait = false, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<QueueDeclareOk> QueueDeclareAsync(string queue, bool durable, bool exclusive, bool autoDelete, IDictionary<string, object?>? arguments = null, bool passive = false, bool noWait = false, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<QueueDeclareOk> QueueDeclarePassiveAsync(string queue, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<uint> QueueDeleteAsync(string queue, bool ifUnused, bool ifEmpty, bool noWait = false, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task QueueBindAsync(string queue, string exchange, string routingKey, IDictionary<string, object?>? arguments = null, bool noWait = false, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task QueueUnbindAsync(string queue, string exchange, string routingKey, IDictionary<string, object?>? arguments = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<uint> QueuePurgeAsync(string queue, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task TxSelectAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task TxCommitAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task TxRollbackAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task CloseAsync(ushort replyCode, string replyText, bool abort, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task CloseAsync(ShutdownEventArgs reason, bool abort) => Task.CompletedTask;
        public Task CloseAsync(ShutdownEventArgs reason, bool abort, CancellationToken cancellationToken) => Task.CompletedTask;

        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
