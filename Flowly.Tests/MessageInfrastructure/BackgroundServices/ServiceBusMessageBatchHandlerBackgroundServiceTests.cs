using Flowly.MessageInfrastructure.BackgroundServices;
using Flowly.MessageInfrastructure.Model;
using Flowly.MessageInfrastructure.Receivers;
using Flowly.MessageInfrastructure.Telemetry;
using Flowly.MessagingAbstractions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Flowly.Tests.MessageInfrastructure.BackgroundServices;

public class ServiceBusMessageBatchHandlerBackgroundServiceTests
{
    public class Execute
    {
        [Fact]
        public async Task CreatesReceiverWithQueueNameFromSettings()
        {
            var (serviceBusMessageBatchHandlerBackgroundService, client, _) = Build("batch-queue");

            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
            await serviceBusMessageBatchHandlerBackgroundService.StartAsync(cts.Token);
            await Task.Delay(100);
            await serviceBusMessageBatchHandlerBackgroundService.StopAsync(CancellationToken.None);

            Assert.Equal("batch-queue", client.CreatedReceiverQueueName);
        }

        [Fact]
        public async Task ReceivesMessagesWithSettingsFromBatchQueueSettings()
        {
            var (serviceBusMessageBatchHandlerBackgroundService, _, receiver) = Build("batch-queue", maxMessages: 25, maxWaitTime: TimeSpan.FromSeconds(10));

            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
            await serviceBusMessageBatchHandlerBackgroundService.StartAsync(cts.Token);
            await Task.Delay(100);
            await serviceBusMessageBatchHandlerBackgroundService.StopAsync(CancellationToken.None);

            Assert.Equal(25, receiver.LastMaxMessages);
            Assert.Equal(TimeSpan.FromSeconds(10), receiver.LastMaxWaitTime);
        }

        [Fact]
        public async Task WhenNoMessagesReceived_DoesNotInvokeHandler()
        {
            var handler = new RecordingBatchHandler();
            var (serviceBusMessageBatchHandlerBackgroundService, _, _) = Build(handler: handler);

            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
            await serviceBusMessageBatchHandlerBackgroundService.StartAsync(cts.Token);
            await Task.Delay(100);
            await serviceBusMessageBatchHandlerBackgroundService.StopAsync(CancellationToken.None);

            Assert.Equal(0, handler.HandleCallCount);
        }

        [Fact]
        public async Task WhenMessagesReceived_InvokesHandlerWithMessageBodies()
        {
            var handler = new RecordingBatchHandler();
            var messages = new[] { new FakeReceivedMessage<TestMessage>(new TestMessage("a")), new FakeReceivedMessage<TestMessage>(new TestMessage("b")) };
            var (serviceBusMessageBatchHandlerBackgroundService, _, receiver) = Build(messages: messages, handler: handler);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await serviceBusMessageBatchHandlerBackgroundService.StartAsync(cts.Token);
            await receiver.BatchCompleted.WaitAsync(cts.Token);
            await serviceBusMessageBatchHandlerBackgroundService.StopAsync(CancellationToken.None);

            Assert.Equal([new TestMessage("a"), new TestMessage("b")], handler.ReceivedMessages);
        }

        [Fact]
        public async Task WhenMessagesReceived_CompletesMessagesAfterHandling()
        {
            var messages = new[] { new FakeReceivedMessage<TestMessage>(new TestMessage("x")) };
            var (serviceBusMessageBatchHandlerBackgroundService, _, receiver) = Build(messages: messages);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await serviceBusMessageBatchHandlerBackgroundService.StartAsync(cts.Token);
            await receiver.BatchCompleted.WaitAsync(cts.Token);
            await serviceBusMessageBatchHandlerBackgroundService.StopAsync(CancellationToken.None);

            Assert.True(receiver.CompleteWasCalled);
        }

        [Fact]
        public async Task WhenHandlerThrows_ContinuesProcessingNextBatch()
        {
            var messages = new[] { new FakeReceivedMessage<TestMessage>(new TestMessage("y")) };
            var (serviceBusMessageBatchHandlerBackgroundService, _, receiver) = Build(messages: messages, handler: new ThrowingBatchHandler());

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await serviceBusMessageBatchHandlerBackgroundService.StartAsync(cts.Token);
            await receiver.BatchServed.WaitAsync(cts.Token);
            await Task.Delay(50); // allow the catch block to execute

            var stopException = await Record.ExceptionAsync(() => serviceBusMessageBatchHandlerBackgroundService.StopAsync(CancellationToken.None));
            Assert.Null(stopException);
        }
    }

    private static (ServiceBusMessageBatchHandlerBackgroundService<TestMessage> serviceBusMessageBatchHandlerBackgroundService, FakeMessageBusClient client, FakeMessageBusReceiver receiver) Build(
        string queueName = "batch-queue",
        int maxMessages = 10,
        TimeSpan maxWaitTime = default,
        FakeReceivedMessage<TestMessage>[]? messages = null,
        BatchMessageHandlerBase<TestMessage>? handler = null)
    {
        var receiver = new FakeMessageBusReceiver(messages ?? [], stopAfterFirstBatch: messages is { Length: > 0 });
        var client = new FakeMessageBusClient(receiver);
        var settings = new ServiceBusMessageBatchHandlerBackgroundService<TestMessage>.BatchQueueSettings(
            queueName, maxMessages, maxWaitTime == default ? TimeSpan.FromSeconds(1) : maxWaitTime);
        var scopeFactory = new FakeServiceScopeFactory<BatchMessageHandlerBase<TestMessage>>(handler ?? new RecordingBatchHandler());
        var serviceBusMessageBatchHandlerBackgroundService = new ServiceBusMessageBatchHandlerBackgroundService<TestMessage>(
            client, settings, scopeFactory, NullLogger<ServiceBusMessageBatchHandlerBackgroundService<TestMessage>>.Instance, new HandlerInstrumentation(false));
        return (serviceBusMessageBatchHandlerBackgroundService, client, receiver);
    }

    private record TestMessage(string Value);

    private class RecordingBatchHandler : BatchMessageHandlerBase<TestMessage>
    {
        public int HandleCallCount { get; private set; }
        public List<TestMessage> ReceivedMessages { get; } = [];

        public override Task Handle(IBatchMessageContext<TestMessage> ctx)
        {
            HandleCallCount++;
            ReceivedMessages.AddRange(ctx.Messages);
            return Task.CompletedTask;
        }
    }

    private class ThrowingBatchHandler : BatchMessageHandlerBase<TestMessage>
    {
        public override Task Handle(IBatchMessageContext<TestMessage> ctx) =>
            throw new InvalidOperationException("handler error");
    }

    private class FakeMessageBusClient(FakeMessageBusReceiver receiver) : IMessageBusClient
    {
        public string? CreatedReceiverQueueName { get; private set; }

        public Task<IMessageBusReceiver> CreateReceiver(string queueName)
        {
            CreatedReceiverQueueName = queueName;
            return Task.FromResult<IMessageBusReceiver>(receiver);
        }

        public Task<IMessageBusProcessor<TMessage>> CreateProcessor<TMessage>(string queueName, MessageBusProcessorOptions options) => throw new NotImplementedException();
        public Task<IExecutionLaneProcessor> CreateExecutionLaneProcessor(string queueName, string laneFilter, MessageBusProcessorOptions options) => throw new NotImplementedException();
        public Task<IMessageBusSender> CreateMessageBusSender(string queueName) => throw new NotImplementedException();
        public Task<IDeadLetterReceiver> CreateDeadLetterReceiver(string queueName) => throw new NotImplementedException();
        public Task<long> GetDeadLetterMessageCount(string queueName, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private class FakeMessageBusReceiver(
        IReadOnlyCollection<IReceivedMessage<TestMessage>> messages,
        bool stopAfterFirstBatch = false) : IMessageBusReceiver
    {
        public int LastMaxMessages { get; private set; }
        public TimeSpan LastMaxWaitTime { get; private set; }
        public bool CompleteWasCalled { get; private set; }
        public SemaphoreSlim BatchCompleted { get; } = new(0, 1);
        public SemaphoreSlim BatchServed { get; } = new(0, 1);

        private bool _batchServed;

        public Task<IReadOnlyCollection<IReceivedMessage<TMessage>>> ReceiveMessages<TMessage>(
            int maxMessagesBeforeProcessing, TimeSpan maxWaitTime, CancellationToken cancellationToken = default)
        {
            LastMaxMessages = maxMessagesBeforeProcessing;
            LastMaxWaitTime = maxWaitTime;

            if (stopAfterFirstBatch && !_batchServed)
            {
                _batchServed = true;
                if (BatchServed.CurrentCount == 0) BatchServed.Release();
                return Task.FromResult((IReadOnlyCollection<IReceivedMessage<TMessage>>)
                    messages.Cast<IReceivedMessage<TMessage>>().ToList());
            }

            return Task.FromResult<IReadOnlyCollection<IReceivedMessage<TMessage>>>(Array.Empty<IReceivedMessage<TMessage>>());
        }

        public Task CompleteMessages<TMessage>(IReadOnlyCollection<IReceivedMessage<TMessage>> msgs, CancellationToken cancellationToken = default)
        {
            CompleteWasCalled = true;
            if (BatchCompleted.CurrentCount == 0) BatchCompleted.Release();
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
