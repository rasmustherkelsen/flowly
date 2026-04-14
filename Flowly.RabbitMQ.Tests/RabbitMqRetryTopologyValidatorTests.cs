using Flowly.MessagingAbstractions;
using Flowly.RabbitMQ.Tests.Fakes;
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

}
