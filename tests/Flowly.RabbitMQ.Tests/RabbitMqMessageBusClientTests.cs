using Flowly.RabbitMQ.Tests.Fakes;
using Flowly.Transport;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Flowly.RabbitMQ.Tests;

public class RabbitMqMessageBusClientTests
{
    private static (FakeRabbitMqConnectionPool Pool, RabbitMqMessageBusClient Client) Build()
    {
        var pool = new FakeRabbitMqConnectionPool(
            new FakeConnection(new FakeChannel()),
            new FakeConnection(new FakeChannel()));

        return (pool, new RabbitMqMessageBusClient(pool));
    }

    public class CreateReceiver
    {
        [Fact]
        public async Task UsesConsumerConnection()
        {
            var (pool, client) = Build();

            await client.CreateReceiver("test-queue");

            Assert.Equal(1, pool.ConsumerConnection.CreateChannelCallCount);
            Assert.Equal(0, pool.PublisherConnection.CreateChannelCallCount);
        }
    }

    public class CreateProcessor
    {
        [Fact]
        public async Task UsesConsumerConnection()
        {
            var (pool, client) = Build();

            await client.CreateProcessor<TestMessage>("test-queue", new MessageBusProcessorOptions(1, MessageBusReceiveMode.PeekLock));

            Assert.Equal(1, pool.ConsumerConnection.CreateChannelCallCount);
            Assert.Equal(0, pool.PublisherConnection.CreateChannelCallCount);
        }
    }

    public class CreateExecutionLaneProcessor
    {
        [Fact]
        public async Task UsesConsumerConnection()
        {
            var (pool, client) = Build();

            await client.CreateExecutionLaneProcessor("test-queue", "lane-1", new MessageBusProcessorOptions(1, MessageBusReceiveMode.PeekLock));

            Assert.Equal(1, pool.ConsumerConnection.CreateChannelCallCount);
            Assert.Equal(0, pool.PublisherConnection.CreateChannelCallCount);
        }
    }

    public class CreateMessageBusSender
    {
        [Fact]
        public async Task UsesPublisherConnection()
        {
            var (pool, client) = Build();

            await client.CreateMessageBusSender("test-queue");

            Assert.Equal(1, pool.PublisherConnection.CreateChannelCallCount);
            Assert.Equal(0, pool.ConsumerConnection.CreateChannelCallCount);
        }

        [Fact]
        public async Task ReturnsCachedSenderForSameQueue()
        {
            var (pool, client) = Build();

            var sender1 = await client.CreateMessageBusSender("test-queue");
            var sender2 = await client.CreateMessageBusSender("test-queue");

            Assert.Same(sender1, sender2);
            Assert.Equal(1, pool.PublisherConnection.CreateChannelCallCount);
        }

        [Fact]
        public async Task CreatesSeparateSendersForDifferentQueues()
        {
            var (_, client) = Build();

            var sender1 = await client.CreateMessageBusSender("queue-a");
            var sender2 = await client.CreateMessageBusSender("queue-b");

            Assert.NotSame(sender1, sender2);
        }
    }

    public class CreateDeadLetterReceiver
    {
        [Fact]
        public async Task UsesConsumerConnection()
        {
            var (pool, client) = Build();

            await client.CreateDeadLetterReceiver("test-queue");

            Assert.Equal(1, pool.ConsumerConnection.CreateChannelCallCount);
            Assert.Equal(0, pool.PublisherConnection.CreateChannelCallCount);
        }
    }

    public class GetDeadLetterMessageCount
    {
        [Fact]
        public async Task UsesConsumerConnection()
        {
            var (pool, client) = Build();

            await client.GetDeadLetterMessageCount("test-queue");

            Assert.Equal(1, pool.ConsumerConnection.CreateChannelCallCount);
            Assert.Equal(0, pool.PublisherConnection.CreateChannelCallCount);
        }

        [Fact]
        public async Task ReturnsMessageCountFromBroker()
        {
            var channel = new FakeChannel { QueueDeclareResult = new QueueDeclareOk("test-queue.dead-letter", 17, 0) };
            var pool = new FakeRabbitMqConnectionPool(
                new FakeConnection(new FakeChannel()),
                new FakeConnection(channel));
            var client = new RabbitMqMessageBusClient(pool);

            var count = await client.GetDeadLetterMessageCount("test-queue");

            Assert.Equal(17, count);
        }
    }

    private record TestMessage(string Value);

    private class FakeChannel : IChannel
    {
        public QueueDeclareOk QueueDeclareResult { get; set; } = new("", 0, 0);

        public Task<QueueDeclareOk> QueueDeclareAsync(string queue, bool durable, bool exclusive, bool autoDelete, IDictionary<string, object?>? arguments = null, bool passive = false, bool noWait = false, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(QueueDeclareResult);
        }

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

        public ValueTask BasicPublishAsync<TProperties>(string exchange, string routingKey, bool mandatory, TProperties basicProperties, ReadOnlyMemory<byte> body, CancellationToken cancellationToken = default) where TProperties : IReadOnlyBasicProperties, IAmqpHeader
        {
            throw new NotImplementedException();
        }

        public ValueTask BasicPublishAsync<TProperties>(CachedString exchange, CachedString routingKey, bool mandatory, TProperties basicProperties, ReadOnlyMemory<byte> body, CancellationToken cancellationToken = default) where TProperties : IReadOnlyBasicProperties, IAmqpHeader
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