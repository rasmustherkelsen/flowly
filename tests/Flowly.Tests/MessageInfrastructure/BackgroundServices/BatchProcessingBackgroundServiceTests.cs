using System.Diagnostics;
using Flowly.MessageInfrastructure.BackgroundServices;
using Flowly.MessageInfrastructure.Model;
using Flowly.MessageInfrastructure.Telemetry;
using Flowly.Transport;
using Microsoft.Extensions.Logging.Abstractions;

namespace Flowly.Tests.MessageInfrastructure.BackgroundServices;

public class BatchProcessingBackgroundServiceTests
{
    private static (BatchProcessingBackgroundService<TestMessage> batchProcessingBackgroundService, FakeMessageBusClient client, FakeMessageBusReceiver receiver) Build(
        string queueName = "batch-queue",
        int maxMessages = 10,
        TimeSpan maxWaitTime = default,
        int maxRetries = 0,
        int retryDelaySeconds = 0,
        FakeReceivedMessage<TestMessage>[]? messages = null,
        BatchMessageHandler<TestMessage>? handler = null,
        IHandlerInstrumentation? instrumentation = null)
    {
        var receiver = new FakeMessageBusReceiver(messages ?? [], messages is { Length: > 0 });
        var client = new FakeMessageBusClient(receiver);
        var clientRegistry = new FakeMessageBusClientRegistry(client);
        var settings = new HandlerSettings<TestMessage>(
            queueName, "azure-service-bus", "BatchHandler", false, 1, maxRetries, retryDelaySeconds, maxMessages, maxWaitTime == default ? TimeSpan.FromSeconds(1) : maxWaitTime);
        var scopeFactory = new FakeServiceScopeFactory<BatchMessageHandler<TestMessage>>(handler ?? new RecordingBatchHandler());
        var batchProcessingBackgroundService = new BatchProcessingBackgroundService<TestMessage>(
            clientRegistry, settings, scopeFactory, NullLogger<BatchProcessingBackgroundService<TestMessage>>.Instance, instrumentation ?? new NullHandlerInstrumentation());
        return (batchProcessingBackgroundService, client, receiver);
    }

    public class Execute
    {
        [Fact]
        public async Task CreatesReceiverWithQueueNameFromSettings()
        {
            var (batchProcessingBackgroundService, client, receiver) = Build();

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await batchProcessingBackgroundService.StartAsync(CancellationToken.None);
            await receiver.ReceiveCalled.WaitAsync(cts.Token);
            await batchProcessingBackgroundService.StopAsync(CancellationToken.None);

            Assert.Equal("batch-queue", client.CreatedReceiverQueueName);
        }

        [Fact]
        public async Task ReceivesMessagesWithSettingsFromBatchQueueSettings()
        {
            var (batchProcessingBackgroundService, _, receiver) = Build("batch-queue", 25, TimeSpan.FromSeconds(10));

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await batchProcessingBackgroundService.StartAsync(CancellationToken.None);
            await receiver.ReceiveCalled.WaitAsync(cts.Token);
            await batchProcessingBackgroundService.StopAsync(CancellationToken.None);

            Assert.Equal(25, receiver.LastMaxMessages);
            Assert.Equal(TimeSpan.FromSeconds(10), receiver.LastMaxWaitTime);
        }

        [Fact]
        public async Task WhenNoMessagesReceived_DoesNotInvokeHandler()
        {
            var handler = new RecordingBatchHandler();
            var (batchProcessingBackgroundService, _, _) = Build(handler: handler);

            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
            await batchProcessingBackgroundService.StartAsync(cts.Token);
            await Task.Delay(100);
            await batchProcessingBackgroundService.StopAsync(CancellationToken.None);

            Assert.Equal(0, handler.HandleCallCount);
        }

        [Fact]
        public async Task WhenMessagesReceived_InvokesHandlerWithMessageBodies()
        {
            var handler = new RecordingBatchHandler();
            var messages = new[] { new FakeReceivedMessage<TestMessage>(new TestMessage("a")), new FakeReceivedMessage<TestMessage>(new TestMessage("b")) };
            var (batchProcessingBackgroundService, _, receiver) = Build(messages: messages, handler: handler);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await batchProcessingBackgroundService.StartAsync(cts.Token);
            await receiver.BatchCompleted.WaitAsync(cts.Token);
            await batchProcessingBackgroundService.StopAsync(CancellationToken.None);

            Assert.Equal([new TestMessage("a"), new TestMessage("b")], handler.ReceivedMessages);
        }

        [Fact]
        public async Task WhenMessagesReceived_CompletesMessages()
        {
            var messages = new[] { new FakeReceivedMessage<TestMessage>(new TestMessage("x")) };
            var (batchProcessingBackgroundService, _, receiver) = Build(messages: messages);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await batchProcessingBackgroundService.StartAsync(cts.Token);
            await receiver.BatchCompleted.WaitAsync(cts.Token);
            await batchProcessingBackgroundService.StopAsync(CancellationToken.None);

            Assert.True(receiver.CompleteWasCalled);
        }

        [Fact]
        public async Task WhenHandlerThrows_ContinuesProcessingNextBatch()
        {
            var messages = new[] { new FakeReceivedMessage<TestMessage>(new TestMessage("y")) };
            var (batchProcessingBackgroundService, _, receiver) = Build(messages: messages, handler: new ThrowingBatchHandler());

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await batchProcessingBackgroundService.StartAsync(cts.Token);
            await receiver.BatchServed.WaitAsync(cts.Token);
            await Task.Delay(50); // allow the catch block to execute

            var stopException = await Record.ExceptionAsync(() => batchProcessingBackgroundService.StopAsync(CancellationToken.None));
            Assert.Null(stopException);
        }
    }

    public class AtMostOnce
    {
        [Fact]
        public async Task WhenHandlerThrows_MessagesAreStillCompleted()
        {
            var messages = new[] { new FakeReceivedMessage<TestMessage>(new TestMessage("z")) };
            var (batchProcessingBackgroundService, _, receiver) = Build(messages: messages, maxRetries: 0, handler: new ThrowingBatchHandler());

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await batchProcessingBackgroundService.StartAsync(cts.Token);
            await receiver.BatchCompleted.WaitAsync(cts.Token);
            await batchProcessingBackgroundService.StopAsync(CancellationToken.None);

            Assert.True(receiver.CompleteWasCalled);
        }

        [Fact]
        public async Task WhenHandlerThrows_MessagesAreNotRepublished()
        {
            var messages = new[] { new FakeReceivedMessage<TestMessage>(new TestMessage("z")) };
            var (batchProcessingBackgroundService, client, receiver) = Build(messages: messages, maxRetries: 0, handler: new ThrowingBatchHandler());

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await batchProcessingBackgroundService.StartAsync(cts.Token);
            await receiver.BatchCompleted.WaitAsync(cts.Token);
            await batchProcessingBackgroundService.StopAsync(CancellationToken.None);

            Assert.Empty(client.SentMessages);
        }
    }

    public class WithRetryPolicy
    {
        [Fact]
        public async Task WhenHandlerSucceeds_CompletesMessagesAfterHandle()
        {
            var messages = new[] { new FakeReceivedMessage<TestMessage>(new TestMessage("ok")) };
            var (batchProcessingBackgroundService, client, receiver) = Build(messages: messages, maxRetries: 3, retryDelaySeconds: 5);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await batchProcessingBackgroundService.StartAsync(cts.Token);
            await receiver.BatchCompleted.WaitAsync(cts.Token);
            await batchProcessingBackgroundService.StopAsync(CancellationToken.None);

            Assert.True(receiver.CompleteWasCalled);
            Assert.Empty(client.SentMessages);
        }

        [Fact]
        public async Task WhenHandlerThrows_AndRetriesRemain_RepublishesMessagesWithIncrementedRetryCount()
        {
            var messages = new[] { new FakeReceivedMessage<TestMessage>(new TestMessage("fail")) };
            var (batchProcessingBackgroundService, client, receiver) = Build(messages: messages, maxRetries: 3, retryDelaySeconds: 5, handler: new ThrowingBatchHandler());

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await batchProcessingBackgroundService.StartAsync(cts.Token);
            await receiver.BatchCompleted.WaitAsync(cts.Token);
            await batchProcessingBackgroundService.StopAsync(CancellationToken.None);

            Assert.Single(client.SentMessages);
            Assert.Equal(1, client.SentMessages[0].Properties.RetryCount);
        }

        [Fact]
        public async Task WhenHandlerThrows_AndRetriesRemain_CompletesOriginalMessages()
        {
            var messages = new[] { new FakeReceivedMessage<TestMessage>(new TestMessage("fail")) };
            var (batchProcessingBackgroundService, client, receiver) = Build(messages: messages, maxRetries: 3, retryDelaySeconds: 5, handler: new ThrowingBatchHandler());

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await batchProcessingBackgroundService.StartAsync(cts.Token);
            await receiver.BatchCompleted.WaitAsync(cts.Token);
            await batchProcessingBackgroundService.StopAsync(CancellationToken.None);

            Assert.True(receiver.CompleteWasCalled);
        }

        [Fact]
        public async Task WhenHandlerThrows_AndRetriesExhausted_CompletesMessagesWithoutRepublishing()
        {
            var messages = new[] { new FakeReceivedMessage<TestMessage>(new TestMessage("fail"), MessageProperties.Empty with { RetryCount = 3 }) };
            var (batchProcessingBackgroundService, client, receiver) = Build(messages: messages, maxRetries: 3, retryDelaySeconds: 5, handler: new ThrowingBatchHandler());

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await batchProcessingBackgroundService.StartAsync(cts.Token);
            await receiver.BatchCompleted.WaitAsync(cts.Token);
            await batchProcessingBackgroundService.StopAsync(CancellationToken.None);

            Assert.True(receiver.CompleteWasCalled);
            Assert.Empty(client.SentMessages);
        }

        [Fact]
        public async Task WhenHandlerThrows_ScheduledEnqueueTimeIsSetToDelayFromNow()
        {
            var messages = new[] { new FakeReceivedMessage<TestMessage>(new TestMessage("fail")) };
            var before = DateTimeOffset.UtcNow;
            var (batchProcessingBackgroundService, client, receiver) = Build(messages: messages, maxRetries: 3, retryDelaySeconds: 10, handler: new ThrowingBatchHandler());

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await batchProcessingBackgroundService.StartAsync(cts.Token);
            await receiver.BatchCompleted.WaitAsync(cts.Token);
            await batchProcessingBackgroundService.StopAsync(CancellationToken.None);

            var scheduled = client.SentMessages[0].Properties.ScheduledEnqueueTime;
            Assert.NotNull(scheduled);
            Assert.True(scheduled >= before + TimeSpan.FromSeconds(10));
        }
    }

    public class Telemetry
    {
        [Fact]
        public async Task WhenMessagesHaveTraceparent_StartsHandlingWithActivityLinksForEachMessage()
        {
            var traceId1 = ActivityTraceId.CreateRandom();
            var spanId1 = ActivitySpanId.CreateRandom();
            var traceId2 = ActivityTraceId.CreateRandom();
            var spanId2 = ActivitySpanId.CreateRandom();
            var traceparent1 = $"00-{traceId1}-{spanId1}-01";
            var traceparent2 = $"00-{traceId2}-{spanId2}-01";

            var instrumentation = new LinkCapturingHandlerInstrumentation();
            var messages = new[]
            {
                new FakeReceivedMessage<TestMessage>(new TestMessage("a"), MessageProperties.Empty with { Traceparent = traceparent1 }),
                new FakeReceivedMessage<TestMessage>(new TestMessage("b"), MessageProperties.Empty with { Traceparent = traceparent2 }),
            };
            var (batchProcessingBackgroundService, _, receiver) = Build(messages: messages, instrumentation: instrumentation);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await batchProcessingBackgroundService.StartAsync(cts.Token);
            await receiver.BatchCompleted.WaitAsync(cts.Token);
            await batchProcessingBackgroundService.StopAsync(CancellationToken.None);

            Assert.NotNull(instrumentation.CapturedLinks);
            Assert.Equal(2, instrumentation.CapturedLinks.Count);
            Assert.Contains(instrumentation.CapturedLinks, l => l.Context.TraceId == traceId1);
            Assert.Contains(instrumentation.CapturedLinks, l => l.Context.TraceId == traceId2);
        }

        [Fact]
        public async Task WhenMessagesHaveNoTraceparent_StartsHandlingWithNoLinks()
        {
            var instrumentation = new LinkCapturingHandlerInstrumentation();
            var messages = new[] { new FakeReceivedMessage<TestMessage>(new TestMessage("a")) };
            var (batchProcessingBackgroundService, _, receiver) = Build(messages: messages, instrumentation: instrumentation);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await batchProcessingBackgroundService.StartAsync(cts.Token);
            await receiver.BatchCompleted.WaitAsync(cts.Token);
            await batchProcessingBackgroundService.StopAsync(CancellationToken.None);

            Assert.NotNull(instrumentation.CapturedLinks);
            Assert.Empty(instrumentation.CapturedLinks);
        }

        [Fact]
        public async Task WhenSomeMessagesHaveTraceparent_OnlyLinksForTraceparentMessages()
        {
            var traceId = ActivityTraceId.CreateRandom();
            var spanId = ActivitySpanId.CreateRandom();
            var traceparent = $"00-{traceId}-{spanId}-01";

            var instrumentation = new LinkCapturingHandlerInstrumentation();
            var messages = new[]
            {
                new FakeReceivedMessage<TestMessage>(new TestMessage("a"), MessageProperties.Empty with { Traceparent = traceparent }),
                new FakeReceivedMessage<TestMessage>(new TestMessage("b")),
            };
            var (batchProcessingBackgroundService, _, receiver) = Build(messages: messages, instrumentation: instrumentation);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await batchProcessingBackgroundService.StartAsync(cts.Token);
            await receiver.BatchCompleted.WaitAsync(cts.Token);
            await batchProcessingBackgroundService.StopAsync(CancellationToken.None);

            Assert.NotNull(instrumentation.CapturedLinks);
            Assert.Single(instrumentation.CapturedLinks);
            Assert.Equal(traceId, instrumentation.CapturedLinks[0].Context.TraceId);
        }
    }

    private sealed class LinkCapturingHandlerInstrumentation : IHandlerInstrumentation
    {
        public List<ActivityLink>? CapturedLinks { get; private set; }

        public bool IsEnabled => true;

        public Activity? StartHandling(string handlerName, string queueName, string messagingSystem, MessageProperties messageProperties, ActivityContext parentContext = default)
            => null;

        public Activity? StartHandling(string handlerName, string queueName, string messagingSystem, MessageProperties messageProperties, IEnumerable<ActivityLink> links)
        {
            CapturedLinks = links.ToList();
            return null;
        }

        public void RecordReceived(string handlerName, string queueName, long count = 1) { }
        public void RecordSucceeded(string handlerName, string queueName, double durationMs, long count = 1) { }
        public void RecordFailed(string handlerName, string queueName, long count = 1) { }
        public void RecordRetried(string handlerName, string queueName, long count = 1) { }

        public void RecordHalted(string handlerName, string queueName, string reason) { }
        public Activity? StartSendingResponse(string callQueueName, string replyQueueName, string messagingSystem, string messageId, string correlationId) => null;
        public void RecordResponseSent(string callQueueName, double durationMs) { }
        public void RecordResponseFailed(string callQueueName) { }
    }

    private record TestMessage(string Value);

    private class RecordingBatchHandler : BatchMessageHandler<TestMessage>
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

    private class ThrowingBatchHandler : BatchMessageHandler<TestMessage>
    {
        public override Task Handle(IBatchMessageContext<TestMessage> ctx)
        {
            throw new InvalidOperationException("handler error");
        }
    }

    private class FakeMessageBusClient(FakeMessageBusReceiver receiver) : IMessageBusClient
    {
        public string? CreatedReceiverQueueName { get; private set; }
        public string MessagingSystem => "fake";
        public List<(object Body, MessageProperties Properties)> SentMessages { get; } = [];

        public Task<IMessageBusReceiver> CreateReceiver(string queueName)
        {
            CreatedReceiverQueueName = queueName;
            return Task.FromResult<IMessageBusReceiver>(receiver);
        }

        public Task<IMessageBusProcessor<TMessage>> CreateProcessor<TMessage>(string queueName, MessageBusProcessorOptions options)
        {
            throw new NotImplementedException();
        }

        public Task<IExecutionLaneProcessor> CreateExecutionLaneProcessor(string queueName, string laneFilter, MessageBusProcessorOptions options)
        {
            throw new NotImplementedException();
        }

        public Task<IMessageBusSender> CreateMessageBusSender(string queueName)
        {
            return Task.FromResult<IMessageBusSender>(new FakeMessageBusSender(SentMessages));
        }

        public Task<IDeadLetterReceiver> CreateDeadLetterReceiver(string queueName)
        {
            throw new NotImplementedException();
        }

        public Task<long> GetDeadLetterMessageCount(string queueName, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }

    private class FakeMessageBusSender(List<(object Body, MessageProperties Properties)> sentMessages) : IMessageBusSender
    {
        public Task SendMessage<TMessage>(TMessage message, MessageProperties properties, CancellationToken cancellationToken = default)
        {
            sentMessages.Add((message!, properties));
            return Task.CompletedTask;
        }

        public Task SendEmptyMessage(MessageProperties messageProperties, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task SendRawMessage(string rawBody, IReadOnlyDictionary<string, object> applicationProperties, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private class FakeMessageBusReceiver(
        IReadOnlyCollection<IReceivedMessage<TestMessage>> messages,
        bool stopAfterFirstBatch = false) : IMessageBusReceiver
    {
        private bool _batchServed;
        public int LastMaxMessages { get; private set; }
        public TimeSpan LastMaxWaitTime { get; private set; }
        public bool CompleteWasCalled { get; private set; }
        public SemaphoreSlim BatchCompleted { get; } = new(0, 1);
        public SemaphoreSlim BatchServed { get; } = new(0, 1);
        public SemaphoreSlim ReceiveCalled { get; } = new(0, 1);

        public Task<IReadOnlyCollection<IReceivedMessage<TMessage>>> ReceiveMessages<TMessage>(
            int maxMessagesBeforeProcessing, TimeSpan maxWaitTime, CancellationToken cancellationToken = default)
        {
            LastMaxMessages = maxMessagesBeforeProcessing;
            LastMaxWaitTime = maxWaitTime;
            if (ReceiveCalled.CurrentCount == 0) ReceiveCalled.Release();

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

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}
