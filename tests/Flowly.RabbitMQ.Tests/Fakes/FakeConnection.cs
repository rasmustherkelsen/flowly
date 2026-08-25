using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Flowly.RabbitMQ.Tests.Fakes;

internal class FakeConnection(IChannel channel) : IConnection
{
    public int CreateChannelCallCount { get; private set; }
    public CreateChannelOptions? LastCreateChannelOptions { get; private set; }

    public Task<IChannel> CreateChannelAsync(CreateChannelOptions? options = null, CancellationToken cancellationToken = default)
    {
        CreateChannelCallCount++;
        LastCreateChannelOptions = options;
        return Task.FromResult(channel);
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
