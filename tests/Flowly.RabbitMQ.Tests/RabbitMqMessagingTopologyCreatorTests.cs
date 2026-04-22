using Flowly.RabbitMQ.Tests.Fakes;
using Flowly.Transport;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Flowly.RabbitMQ.Tests;

public class RabbitMqMessagingTopologyCreatorTests
{
    public class CreateTopology
    {
        [Fact]
        public async Task PassesCancellationTokenToConnectionPool()
        {
            using var cts = new CancellationTokenSource();
            var pool = new SpyConnectionPool(new TopologyChannel());
            var creator = new RabbitMqMessagingTopologyCreator(pool);

            await creator.CreateTopology([new FakeQueueDescription("order-placed")], cts.Token);

            Assert.Equal(cts.Token, pool.ReceivedCancellationToken);
        }

        [Fact]
        public async Task PassesCancellationTokenToChannelCreation()
        {
            using var cts = new CancellationTokenSource();
            var pool = new SpyConnectionPool(new TopologyChannel());
            var creator = new RabbitMqMessagingTopologyCreator(pool);

            await creator.CreateTopology([new FakeQueueDescription("order-placed")], cts.Token);

            Assert.Equal(cts.Token, pool.Connection.ReceivedCancellationToken);
        }

        [Fact]
        public async Task PassesCancellationTokenToAllChannelOperations()
        {
            using var cts = new CancellationTokenSource();
            var channel = new TopologyChannel();
            var pool = new SpyConnectionPool(channel);
            var creator = new RabbitMqMessagingTopologyCreator(pool);

            await creator.CreateTopology([new FakeQueueDescription("order-placed")], cts.Token);

            Assert.NotEmpty(channel.ReceivedCancellationTokens);
            Assert.All(channel.ReceivedCancellationTokens, token => Assert.Equal(cts.Token, token));
        }
    }

    private record FakeQueueDescription(string Name) : IQueueDescription
    {
        public TimeSpan DefaultMessageTimeToLive => TimeSpan.FromDays(1);
        public bool DeadLetterOnMessageExpiration => true;
        public TimeSpan LockDuration => TimeSpan.FromMinutes(5);
        public bool RequiresSession => false;
    }

    private sealed class SpyConnectionPool(TopologyChannel channel) : IRabbitMqConnectionPool
    {
        public CancellationToken ReceivedCancellationToken { get; private set; }
        public SpyConnection Connection { get; } = new(channel);

        public Task<IConnection> GetPublisherConnection(CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<IConnection> GetConsumerConnection(CancellationToken cancellationToken = default)
        {
            ReceivedCancellationToken = cancellationToken;
            return Task.FromResult<IConnection>(Connection);
        }
    }

    private sealed class SpyConnection(TopologyChannel channel) : IConnection
    {
        public CancellationToken ReceivedCancellationToken { get; private set; }

        public Task<IChannel> CreateChannelAsync(CreateChannelOptions? options = null, CancellationToken cancellationToken = default)
        {
            ReceivedCancellationToken = cancellationToken;
            return Task.FromResult<IChannel>(channel);
        }

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

        public Task UpdateSecretAsync(string newSecret, string reason, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task CloseAsync(ushort reasonCode, string reasonText, TimeSpan timeout, bool abort, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public event AsyncEventHandler<CallbackExceptionEventArgs>? CallbackExceptionAsync
        {
            add { }
            remove { }
        }

        public event AsyncEventHandler<ShutdownEventArgs>? ConnectionShutdownAsync
        {
            add { }
            remove { }
        }

        public event AsyncEventHandler<AsyncEventArgs>? RecoverySucceededAsync
        {
            add { }
            remove { }
        }

        public event AsyncEventHandler<ConnectionRecoveryErrorEventArgs>? ConnectionRecoveryErrorAsync
        {
            add { }
            remove { }
        }

        public event AsyncEventHandler<ConsumerTagChangedAfterRecoveryEventArgs>? ConsumerTagChangeAfterRecoveryAsync
        {
            add { }
            remove { }
        }

        public event AsyncEventHandler<QueueNameChangedAfterRecoveryEventArgs>? QueueNameChangedAfterRecoveryAsync
        {
            add { }
            remove { }
        }

        public event AsyncEventHandler<RecoveringConsumerEventArgs>? RecoveringConsumerAsync
        {
            add { }
            remove { }
        }

        public event AsyncEventHandler<ConnectionBlockedEventArgs>? ConnectionBlockedAsync
        {
            add { }
            remove { }
        }

        public event AsyncEventHandler<AsyncEventArgs>? ConnectionUnblockedAsync
        {
            add { }
            remove { }
        }

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }

    private sealed class TopologyChannel : ChannelStub
    {
        public List<CancellationToken> ReceivedCancellationTokens { get; } = [];

        public override Task ExchangeDeclareAsync(
            string exchange, string type, bool durable, bool autoDelete,
            IDictionary<string, object?>? arguments = null, bool passive = false, bool noWait = false,
            CancellationToken cancellationToken = default)
        {
            ReceivedCancellationTokens.Add(cancellationToken);
            return Task.CompletedTask;
        }

        public override Task<QueueDeclareOk> QueueDeclareAsync(
            string queue, bool durable, bool exclusive, bool autoDelete,
            IDictionary<string, object?>? arguments = null, bool passive = false, bool noWait = false,
            CancellationToken cancellationToken = default)
        {
            ReceivedCancellationTokens.Add(cancellationToken);
            return Task.FromResult(new QueueDeclareOk(queue, 0, 0));
        }

        public override Task QueueBindAsync(
            string queue, string exchange, string routingKey,
            IDictionary<string, object?>? arguments = null, bool noWait = false,
            CancellationToken cancellationToken = default)
        {
            ReceivedCancellationTokens.Add(cancellationToken);
            return Task.CompletedTask;
        }
    }
}