using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Flowly.RabbitMQ.Tests.Fakes;

internal class ChannelStub : IChannel
{
    public bool WasClosed { get; private set; }
    public bool WasDisposed { get; private set; }

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

    public virtual ValueTask BasicAckAsync(ulong deliveryTag, bool multiple, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public virtual ValueTask BasicNackAsync(ulong deliveryTag, bool multiple, bool requeue, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public virtual ValueTask BasicRejectAsync(ulong deliveryTag, bool requeue, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public virtual Task BasicQosAsync(uint prefetchSize, ushort prefetchCount, bool global, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public virtual ValueTask BasicPublishAsync<TProperties>(string exchange, string routingKey, bool mandatory, TProperties basicProperties, ReadOnlyMemory<byte> body, CancellationToken cancellationToken = default)
        where TProperties : IReadOnlyBasicProperties, IAmqpHeader
        => throw new NotImplementedException();

    public virtual ValueTask BasicPublishAsync<TProperties>(CachedString exchange, CachedString routingKey, bool mandatory, TProperties basicProperties, ReadOnlyMemory<byte> body, CancellationToken cancellationToken = default)
        where TProperties : IReadOnlyBasicProperties, IAmqpHeader
        => throw new NotImplementedException();

    public virtual Task<string> BasicConsumeAsync(string queue, bool autoAck, string consumerTag, bool noLocal, bool exclusive, IDictionary<string, object?>? arguments, IAsyncBasicConsumer consumer, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public virtual Task BasicCancelAsync(string consumerTag, bool noWait = false, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public virtual Task<BasicGetResult?> BasicGetAsync(string queue, bool autoAck, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public virtual ValueTask<ulong> GetNextPublishSequenceNumberAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public virtual Task<uint> ConsumerCountAsync(string queue, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public virtual Task<uint> MessageCountAsync(string queue, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public virtual Task ExchangeDeclareAsync(string exchange, string type, bool durable, bool autoDelete, IDictionary<string, object?>? arguments = null, bool passive = false, bool noWait = false, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public virtual Task ExchangeDeclarePassiveAsync(string exchange, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public virtual Task ExchangeDeleteAsync(string exchange, bool ifUnused, bool noWait = false, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public virtual Task ExchangeBindAsync(string destination, string source, string routingKey, IDictionary<string, object?>? arguments = null, bool noWait = false, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public virtual Task ExchangeUnbindAsync(string destination, string source, string routingKey, IDictionary<string, object?>? arguments = null, bool noWait = false, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public virtual Task<QueueDeclareOk> QueueDeclareAsync(string queue, bool durable, bool exclusive, bool autoDelete, IDictionary<string, object?>? arguments = null, bool passive = false, bool noWait = false, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public virtual Task<QueueDeclareOk> QueueDeclarePassiveAsync(string queue, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public virtual Task<uint> QueueDeleteAsync(string queue, bool ifUnused, bool ifEmpty, bool noWait = false, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public virtual Task QueueBindAsync(string queue, string exchange, string routingKey, IDictionary<string, object?>? arguments = null, bool noWait = false, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public virtual Task QueueUnbindAsync(string queue, string exchange, string routingKey, IDictionary<string, object?>? arguments = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public virtual Task<uint> QueuePurgeAsync(string queue, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public virtual Task TxSelectAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public virtual Task TxCommitAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public virtual Task TxRollbackAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public virtual Task CloseAsync(ushort replyCode, string replyText, bool abort, CancellationToken cancellationToken = default)
    {
        WasClosed = true;
        return Task.CompletedTask;
    }

    public virtual Task CloseAsync(ShutdownEventArgs reason, bool abort)
    {
        WasClosed = true;
        return Task.CompletedTask;
    }

    public virtual Task CloseAsync(ShutdownEventArgs reason, bool abort, CancellationToken cancellationToken)
    {
        WasClosed = true;
        return Task.CompletedTask;
    }

    public void Dispose() { }

    public virtual ValueTask DisposeAsync()
    {
        WasDisposed = true;
        return ValueTask.CompletedTask;
    }
}
