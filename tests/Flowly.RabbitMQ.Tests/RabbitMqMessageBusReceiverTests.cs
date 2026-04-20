using System.Collections;
using System.Text.Json;
using Flowly.RabbitMQ;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Flowly.RabbitMQ.Tests;

public class RabbitMqMessageBusReceiverTests
{
    public class ReceiveMessages
    {
        [Fact]
        public async Task WhenNoMessagesArrive_ReturnsEmptyAfterTimeout()
        {
            var fakeChannel = new FakeChannel();
            var receiver = new RabbitMqMessageBusReceiver(fakeChannel, "test-queue");

            var result = await receiver.ReceiveMessages<TestMessage>(
                maxMessagesBeforeProcessing: 10,
                maxWaitTime: TimeSpan.FromMilliseconds(50));

            Assert.Empty(result);
        }

        [Fact]
        public async Task WhenMessagesArriveWithinTimeout_ReturnsDeliveredMessages()
        {
            var fakeChannel = new FakeChannel();
            var receiver = new RabbitMqMessageBusReceiver(fakeChannel, "test-queue");

            _ = Task.Run(async () =>
            {
                await fakeChannel.WaitForConsumerAsync();
                await fakeChannel.DeliverAsync(1, new TestMessage("hello"));
                await fakeChannel.DeliverAsync(2, new TestMessage("world"));
            });

            var result = await receiver.ReceiveMessages<TestMessage>(
                maxMessagesBeforeProcessing: 2,
                maxWaitTime: TimeSpan.FromSeconds(5));

            Assert.Equal(2, result.Count);
            Assert.Equal("hello", result.ElementAt(0).Body.Value);
            Assert.Equal("world", result.ElementAt(1).Body.Value);
        }

        [Fact]
        public async Task WhenBatchIsFull_ReturnsWithoutWaitingForTimeout()
        {
            var fakeChannel = new FakeChannel();
            var receiver = new RabbitMqMessageBusReceiver(fakeChannel, "test-queue");

            _ = Task.Run(async () =>
            {
                await fakeChannel.WaitForConsumerAsync();
                await fakeChannel.DeliverAsync(1, new TestMessage("a"));
                await fakeChannel.DeliverAsync(2, new TestMessage("b"));
            });

            var result = await receiver.ReceiveMessages<TestMessage>(
                maxMessagesBeforeProcessing: 2,
                maxWaitTime: TimeSpan.FromSeconds(30));

            Assert.Equal(2, result.Count);
        }

        [Fact]
        public async Task WhenCancelled_ReturnsMessagesCollectedSoFar()
        {
            var fakeChannel = new FakeChannel();
            var receiver = new RabbitMqMessageBusReceiver(fakeChannel, "test-queue");

            using var cts = new CancellationTokenSource();

            _ = Task.Run(async () =>
            {
                await fakeChannel.WaitForConsumerAsync();
                await fakeChannel.DeliverAsync(1, new TestMessage("only-one"));
                cts.Cancel();
            });

            var result = await receiver.ReceiveMessages<TestMessage>(
                maxMessagesBeforeProcessing: 10,
                maxWaitTime: TimeSpan.FromSeconds(5),
                cancellationToken: cts.Token);

            Assert.Single(result);
            Assert.Equal("only-one", result.Single().Body.Value);
        }

        [Fact]
        public async Task SetsQosPrefetchToMaxMessagesBeforeProcessing()
        {
            var fakeChannel = new FakeChannel();
            var receiver = new RabbitMqMessageBusReceiver(fakeChannel, "test-queue");

            await receiver.ReceiveMessages<TestMessage>(
                maxMessagesBeforeProcessing: 15,
                maxWaitTime: TimeSpan.FromMilliseconds(10));

            Assert.Equal(15, fakeChannel.LastPrefetchCount);
        }

        [Fact]
        public async Task CancelsConsumerAfterCollecting()
        {
            var fakeChannel = new FakeChannel();
            var receiver = new RabbitMqMessageBusReceiver(fakeChannel, "test-queue");

            await receiver.ReceiveMessages<TestMessage>(
                maxMessagesBeforeProcessing: 5,
                maxWaitTime: TimeSpan.FromMilliseconds(10));

            Assert.True(fakeChannel.ConsumerWasCancelled);
        }
    }

    private record TestMessage(string Value);

    private class FakeChannel : IChannel
    {
        private IAsyncBasicConsumer? _consumer;
        private string _consumerTag = "fake-consumer-tag";
        private readonly SemaphoreSlim _consumerRegistered = new(0, 1);

        public ushort LastPrefetchCount { get; private set; }
        public bool ConsumerWasCancelled { get; private set; }

        public Task WaitForConsumerAsync() => _consumerRegistered.WaitAsync();

        public async Task DeliverAsync<TBody>(ulong deliveryTag, TBody body)
        {
            var bodyBytes = JsonSerializer.SerializeToUtf8Bytes(body);
            var properties = new BasicProperties();

            await _consumer!.HandleBasicDeliverAsync(
                _consumerTag,
                deliveryTag,
                redelivered: false,
                exchange: "",
                routingKey: "test-queue",
                properties: properties,
                body: bodyBytes,
                cancellationToken: CancellationToken.None);
        }

        public Task BasicQosAsync(uint prefetchSize, ushort prefetchCount, bool global, CancellationToken cancellationToken = default)
        {
            LastPrefetchCount = prefetchCount;
            return Task.CompletedTask;
        }

        public Task<string> BasicConsumeAsync(string queue, bool autoAck, string consumerTag, bool noLocal, bool exclusive, IDictionary<string, object?>? arguments, IAsyncBasicConsumer consumer, CancellationToken cancellationToken = default)
        {
            _consumer = consumer;
            _consumerRegistered.Release();
            return Task.FromResult(_consumerTag);
        }

        public Task BasicCancelAsync(string consumerTag, bool noWait = false, CancellationToken cancellationToken = default)
        {
            ConsumerWasCancelled = true;
            return Task.CompletedTask;
        }

        public Task CloseAsync(ushort replyCode, string replyText, bool abort, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task CloseAsync(ShutdownEventArgs reason, bool abort) => Task.CompletedTask;
        public Task CloseAsync(ShutdownEventArgs reason, bool abort, CancellationToken cancellationToken) => Task.CompletedTask;

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

        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
