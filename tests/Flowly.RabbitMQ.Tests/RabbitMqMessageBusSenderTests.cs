using Flowly.Transport;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Flowly.RabbitMQ.Tests;

public class RabbitMqMessageBusSenderTests
{
    public class SendMessage
    {
        [Fact]
        public async Task WhenNoLimitConfigured_DoesNotThrow()
        {
            var sender = new RabbitMqMessageBusSender("test-queue", new SpyChannel(), null);

            await sender.SendMessage(new TestMessage("hello"), MessageProperties.Empty);
        }

        [Fact]
        public async Task WhenMessageIsWithinLimit_DoesNotThrow()
        {
            var sender = new RabbitMqMessageBusSender("test-queue", new SpyChannel(), 1024);

            await sender.SendMessage(new TestMessage("hello"), MessageProperties.Empty);
        }

        [Fact]
        public async Task WhenMessageExceedsLimit_ThrowsMessageTooLargeException()
        {
            var sender = new RabbitMqMessageBusSender("test-queue", new SpyChannel(), 1);

            var exception = await Assert.ThrowsAsync<MessageTooLargeException>(() => sender.SendMessage(new TestMessage("hello"), MessageProperties.Empty));

            Assert.Equal("test-queue", exception.QueueName);
            Assert.Equal(1, exception.MaxSizeBytes);
            Assert.True(exception.ActualSizeBytes > 1);
        }
    }

    public class SendRawMessage
    {
        [Fact]
        public async Task WhenNoLimitConfigured_DoesNotThrow()
        {
            var sender = new RabbitMqMessageBusSender("test-queue", new SpyChannel(), null);

            await sender.SendRawMessage("hello", new Dictionary<string, object>());
        }

        [Fact]
        public async Task WhenMessageIsWithinLimit_DoesNotThrow()
        {
            var sender = new RabbitMqMessageBusSender("test-queue", new SpyChannel(), 1024);

            await sender.SendRawMessage("hello", new Dictionary<string, object>());
        }

        [Fact]
        public async Task WhenMessageExceedsLimit_ThrowsMessageTooLargeException()
        {
            var sender = new RabbitMqMessageBusSender("test-queue", new SpyChannel(), 1);

            var exception = await Assert.ThrowsAsync<MessageTooLargeException>(() => sender.SendRawMessage("hello", new Dictionary<string, object>()));

            Assert.Equal("test-queue", exception.QueueName);
            Assert.Equal(1, exception.MaxSizeBytes);
            Assert.True(exception.ActualSizeBytes > 1);
        }
    }

    private record TestMessage(string Value);

    private class SpyChannel : IChannel
    {
        public int ChannelNumber => 1;
        public ShutdownEventArgs? CloseReason => null;
        public TimeSpan ContinuationTimeout { get; set; } = TimeSpan.FromSeconds(10);
        public string? CurrentQueue => null;
        public bool IsClosed => false;
        public bool IsOpen => true;
        public IAsyncBasicConsumer? DefaultConsumer { get; set; }

        public event AsyncEventHandler<BasicAckEventArgs>? BasicAcksAsync
        {
            add { }
            remove { }
        }

        public event AsyncEventHandler<BasicNackEventArgs>? BasicNacksAsync
        {
            add { }
            remove { }
        }

        public event AsyncEventHandler<BasicReturnEventArgs>? BasicReturnAsync
        {
            add { }
            remove { }
        }

        public event AsyncEventHandler<CallbackExceptionEventArgs>? CallbackExceptionAsync
        {
            add { }
            remove { }
        }

        public event AsyncEventHandler<ShutdownEventArgs>? ChannelShutdownAsync
        {
            add { }
            remove { }
        }

        public event AsyncEventHandler<FlowControlEventArgs>? FlowControlAsync
        {
            add { }
            remove { }
        }

        public ValueTask BasicAckAsync(ulong deliveryTag, bool multiple, CancellationToken cancellationToken = default)
        {
            return ValueTask.CompletedTask;
        }

        public ValueTask BasicNackAsync(ulong deliveryTag, bool multiple, bool requeue, CancellationToken cancellationToken = default)
        {
            return ValueTask.CompletedTask;
        }

        public ValueTask BasicRejectAsync(ulong deliveryTag, bool requeue, CancellationToken cancellationToken = default)
        {
            return ValueTask.CompletedTask;
        }

        public Task BasicQosAsync(uint prefetchSize, ushort prefetchCount, bool global, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public ValueTask BasicPublishAsync<TProperties>(string exchange, string routingKey, bool mandatory, TProperties basicProperties, ReadOnlyMemory<byte> body, CancellationToken cancellationToken = default)
            where TProperties : IReadOnlyBasicProperties, IAmqpHeader
        {
            return ValueTask.CompletedTask;
        }

        public ValueTask BasicPublishAsync<TProperties>(CachedString exchange, CachedString routingKey, bool mandatory, TProperties basicProperties, ReadOnlyMemory<byte> body, CancellationToken cancellationToken = default)
            where TProperties : IReadOnlyBasicProperties, IAmqpHeader
        {
            return ValueTask.CompletedTask;
        }

        public Task<string> BasicConsumeAsync(string queue, bool autoAck, string consumerTag, bool noLocal, bool exclusive, IDictionary<string, object?>? arguments, IAsyncBasicConsumer consumer, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task BasicCancelAsync(string consumerTag, bool noWait = false, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<BasicGetResult?> BasicGetAsync(string queue, bool autoAck, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public ValueTask<ulong> GetNextPublishSequenceNumberAsync(CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<uint> ConsumerCountAsync(string queue, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<uint> MessageCountAsync(string queue, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task ExchangeDeclareAsync(string exchange, string type, bool durable, bool autoDelete, IDictionary<string, object?>? arguments = null, bool passive = false, bool noWait = false, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task ExchangeDeclarePassiveAsync(string exchange, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task ExchangeDeleteAsync(string exchange, bool ifUnused, bool noWait = false, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task ExchangeBindAsync(string destination, string source, string routingKey, IDictionary<string, object?>? arguments = null, bool noWait = false, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task ExchangeUnbindAsync(string destination, string source, string routingKey, IDictionary<string, object?>? arguments = null, bool noWait = false, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<QueueDeclareOk> QueueDeclareAsync(string queue, bool durable, bool exclusive, bool autoDelete, IDictionary<string, object?>? arguments = null, bool passive = false, bool noWait = false, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<QueueDeclareOk> QueueDeclarePassiveAsync(string queue, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<uint> QueueDeleteAsync(string queue, bool ifUnused, bool ifEmpty, bool noWait = false, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task QueueBindAsync(string queue, string exchange, string routingKey, IDictionary<string, object?>? arguments = null, bool noWait = false, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task QueueUnbindAsync(string queue, string exchange, string routingKey, IDictionary<string, object?>? arguments = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<uint> QueuePurgeAsync(string queue, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task TxSelectAsync(CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task TxCommitAsync(CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task TxRollbackAsync(CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task CloseAsync(ushort replyCode, string replyText, bool abort, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task CloseAsync(ShutdownEventArgs reason, bool abort)
        {
            return Task.CompletedTask;
        }

        public Task CloseAsync(ShutdownEventArgs reason, bool abort, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}