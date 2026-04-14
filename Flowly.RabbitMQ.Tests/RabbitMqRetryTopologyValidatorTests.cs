using Flowly.MessagingAbstractions;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;

namespace Flowly.RabbitMQ.Tests;

public class RabbitMqRetryTopologyValidatorTests
{
    public class Validate
    {
        [Fact]
        public async Task WhenRetryQueueExists_DoesNotThrow()
        {
            var pool = new FakeConnectionPool(new SucceedingChannel());
            var validator = new RabbitMqRetryTopologyValidator("rabbitmq", pool);

            await validator.Validate([new FakeQueueDescription("order-created")], CancellationToken.None);
        }

        [Fact]
        public async Task WhenRetryQueueMissing_ThrowsWithQueueNameInMessage()
        {
            var pool = new FakeConnectionPool(new FailingChannel());
            var validator = new RabbitMqRetryTopologyValidator("rabbitmq", pool);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => validator.Validate([new FakeQueueDescription("order-created")], CancellationToken.None));

            Assert.Contains("order-created.retry", exception.Message);
        }

        [Fact]
        public async Task WhenSecondRetryQueueMissing_ThrowsNamingThatQueue()
        {
            var pool = new FakeConnectionPool(new SucceedingChannel(), new FailingChannel());
            var validator = new RabbitMqRetryTopologyValidator("rabbitmq", pool);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => validator.Validate(
                    [new FakeQueueDescription("order-created"), new FakeQueueDescription("invoice-generated")],
                    CancellationToken.None));

            Assert.Contains("invoice-generated.retry", exception.Message);
        }
    }

    private record FakeQueueDescription(string Name) : IQueueDescription
    {
        public TimeSpan DefaultMessageTimeToLive => TimeSpan.FromDays(1);
        public bool DeadLetterOnMessageExpiration => true;
        public TimeSpan LockDuration => TimeSpan.FromMinutes(5);
        public bool RequiresSession => false;
    }

    private class FakeConnectionPool(params IChannel[] channels) : IRabbitMqConnectionPool
    {
        public Task<IConnection> GetPublisherConnection(CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<IConnection> GetConsumerConnection(CancellationToken cancellationToken = default)
            => Task.FromResult<IConnection>(new SequencedConnection(channels));
    }

    private class SequencedConnection(IChannel[] channels) : IConnection
    {
        private int _index;

        public Task<IChannel> CreateChannelAsync(CreateChannelOptions? options = null, CancellationToken cancellationToken = default)
            => Task.FromResult(channels[_index++]);

        public bool IsOpen => true;
        public int LocalPort => 0;
        public int RemotePort => 0;
        public ushort ChannelMax => 0;
        public IDictionary<string, object?> ClientProperties => new Dictionary<string, object?>();
        public ShutdownEventArgs? CloseReason => null;
        public AmqpTcpEndpoint Endpoint => throw new NotImplementedException();
        public uint FrameMax => 0;
        public TimeSpan Heartbeat => TimeSpan.Zero;
        public IProtocol Protocol => throw new NotImplementedException();
        public IDictionary<string, object?>? ServerProperties => null;
        public IEnumerable<ShutdownReportEntry> ShutdownReport => [];
        public string ClientProvidedName => string.Empty;

        public Task UpdateSecretAsync(string newSecret, string reason, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task CloseAsync(ushort reasonCode, string reasonText, TimeSpan timeout, bool abort, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public event AsyncEventHandler<CallbackExceptionEventArgs>? CallbackExceptionAsync { add { } remove { } }
        public event AsyncEventHandler<ShutdownEventArgs>? ConnectionShutdownAsync { add { } remove { } }
        public event AsyncEventHandler<AsyncEventArgs>? RecoverySucceededAsync { add { } remove { } }
        public event AsyncEventHandler<ConnectionRecoveryErrorEventArgs>? ConnectionRecoveryErrorAsync { add { } remove { } }
        public event AsyncEventHandler<ConsumerTagChangedAfterRecoveryEventArgs>? ConsumerTagChangeAfterRecoveryAsync { add { } remove { } }
        public event AsyncEventHandler<QueueNameChangedAfterRecoveryEventArgs>? QueueNameChangedAfterRecoveryAsync { add { } remove { } }
        public event AsyncEventHandler<RecoveringConsumerEventArgs>? RecoveringConsumerAsync { add { } remove { } }
        public event AsyncEventHandler<ConnectionBlockedEventArgs>? ConnectionBlockedAsync { add { } remove { } }
        public event AsyncEventHandler<AsyncEventArgs>? ConnectionUnblockedAsync { add { } remove { } }

        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private class SucceedingChannel : ChannelStub
    {
        public override Task<QueueDeclareOk> QueueDeclarePassiveAsync(string queue, CancellationToken cancellationToken = default)
            => Task.FromResult(new QueueDeclareOk(queue, 0, 0));
    }

    private class FailingChannel : ChannelStub
    {
        public override Task<QueueDeclareOk> QueueDeclarePassiveAsync(string queue, CancellationToken cancellationToken = default)
            => throw new OperationInterruptedException(
                new ShutdownEventArgs(ShutdownInitiator.Peer, 404, $"NOT_FOUND - no queue '{queue}'"));
    }

    private abstract class ChannelStub : IChannel
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

        public ValueTask BasicAckAsync(ulong deliveryTag, bool multiple, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public ValueTask BasicNackAsync(ulong deliveryTag, bool multiple, bool requeue, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public ValueTask BasicRejectAsync(ulong deliveryTag, bool requeue, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task BasicQosAsync(uint prefetchSize, ushort prefetchCount, bool global, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public ValueTask BasicPublishAsync<TProperties>(string exchange, string routingKey, bool mandatory, TProperties basicProperties, ReadOnlyMemory<byte> body, CancellationToken cancellationToken = default)
            where TProperties : IReadOnlyBasicProperties, IAmqpHeader
            => throw new NotImplementedException();

        public ValueTask BasicPublishAsync<TProperties>(CachedString exchange, CachedString routingKey, bool mandatory, TProperties basicProperties, ReadOnlyMemory<byte> body, CancellationToken cancellationToken = default)
            where TProperties : IReadOnlyBasicProperties, IAmqpHeader
            => throw new NotImplementedException();

        public Task<string> BasicConsumeAsync(string queue, bool autoAck, string consumerTag, bool noLocal, bool exclusive, IDictionary<string, object?>? arguments, IAsyncBasicConsumer consumer, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task BasicCancelAsync(string consumerTag, bool noWait = false, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<BasicGetResult?> BasicGetAsync(string queue, bool autoAck, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public ValueTask<ulong> GetNextPublishSequenceNumberAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<uint> ConsumerCountAsync(string queue, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<uint> MessageCountAsync(string queue, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task ExchangeDeclareAsync(string exchange, string type, bool durable, bool autoDelete, IDictionary<string, object?>? arguments = null, bool passive = false, bool noWait = false, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task ExchangeDeclarePassiveAsync(string exchange, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task ExchangeDeleteAsync(string exchange, bool ifUnused, bool noWait = false, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task ExchangeBindAsync(string destination, string source, string routingKey, IDictionary<string, object?>? arguments = null, bool noWait = false, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task ExchangeUnbindAsync(string destination, string source, string routingKey, IDictionary<string, object?>? arguments = null, bool noWait = false, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<QueueDeclareOk> QueueDeclareAsync(string queue, bool durable, bool exclusive, bool autoDelete, IDictionary<string, object?>? arguments = null, bool passive = false, bool noWait = false, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public abstract Task<QueueDeclareOk> QueueDeclarePassiveAsync(string queue, CancellationToken cancellationToken = default);
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
