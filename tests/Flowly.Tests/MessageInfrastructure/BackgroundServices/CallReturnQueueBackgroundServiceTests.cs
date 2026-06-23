using System.Diagnostics;
using Flowly.MessageInfrastructure.BackgroundServices;
using Flowly.MessageInfrastructure.Callers;
using Flowly.MessageInfrastructure.Registration;
using Flowly.MessageInfrastructure.Telemetry;
using Flowly.Transport;
using Microsoft.Extensions.Logging.Abstractions;

namespace Flowly.Tests.MessageInfrastructure.BackgroundServices;

public class CallReturnQueueBackgroundServiceTests
{
    public class OnMessage
    {
        [Fact]
        public async Task StartsReceivingResponseWithCorrectArgs()
        {
            var instrumentation = new CapturingCallerInstrumentation();
            var (service, processor, _) = Build(instrumentation: instrumentation);

            await service.StartAsync(CancellationToken.None);
            var started = await processor.Started;
            await started;

            var message = new FakeReceivedMessage<PongMessage>(
                new PongMessage("echo"),
                new MessageProperties("msg-abc", "corr-xyz"));

            await processor.FireProcessMessage(message);
            await service.StopAsync(CancellationToken.None);

            Assert.Single(instrumentation.ResponseStarted);
            Assert.Equal("ping-reply", instrumentation.ResponseStarted[0].ReplyQueueName);
            Assert.Equal("stub", instrumentation.ResponseStarted[0].MessagingSystem);
            Assert.Equal("msg-abc", instrumentation.ResponseStarted[0].MessageId);
            Assert.Equal("corr-xyz", instrumentation.ResponseStarted[0].CorrelationId);
        }

        [Fact]
        public async Task ParsesTraceparentAsParentContext()
        {
            var instrumentation = new CapturingCallerInstrumentation();
            var (service, processor, _) = Build(instrumentation: instrumentation);

            await service.StartAsync(CancellationToken.None);
            var started = await processor.Started;
            await started;

            var traceId = ActivityTraceId.CreateRandom();
            var spanId = ActivitySpanId.CreateRandom();
            var traceparent = $"00-{traceId}-{spanId}-01";

            var message = new FakeReceivedMessage<PongMessage>(
                new PongMessage("echo"),
                new MessageProperties("msg-1", "corr-1", Traceparent: traceparent));

            await processor.FireProcessMessage(message);
            await service.StopAsync(CancellationToken.None);

            Assert.Single(instrumentation.ResponseStarted);
            Assert.Equal(traceId, instrumentation.ResponseStarted[0].ParentContext.TraceId);
        }

        [Fact]
        public async Task WhenNoPendingCall_DoesNotThrow()
        {
            var registry = new CapturingPendingCallRegistry(resolves: false);
            var (service, processor, _) = Build(pendingCallRegistry: registry);

            await service.StartAsync(CancellationToken.None);
            var started = await processor.Started;
            await started;

            var message = new FakeReceivedMessage<PongMessage>(
                new PongMessage("echo"),
                new MessageProperties("msg-1", "orphan-corr"));

            var exception = await Record.ExceptionAsync(() => processor.FireProcessMessage(message));
            await service.StopAsync(CancellationToken.None);

            Assert.Null(exception);
        }

        [Fact]
        public async Task ResolvesResponseViaPendingCallRegistry()
        {
            var registry = new CapturingPendingCallRegistry(resolves: true);
            var (service, processor, _) = Build(pendingCallRegistry: registry);

            await service.StartAsync(CancellationToken.None);
            var started = await processor.Started;
            await started;

            var pong = new PongMessage("result");
            var message = new FakeReceivedMessage<PongMessage>(
                pong,
                new MessageProperties("msg-1", "corr-abc"));

            await processor.FireProcessMessage(message);
            await service.StopAsync(CancellationToken.None);

            Assert.Equal("corr-abc", registry.ResolvedCorrelationId);
            Assert.Same(pong, registry.ResolvedBody);
        }
    }

    private static (CallReturnQueueBackgroundService<PongMessage> service, FakeReturnQueueProcessor processor, FakeReturnQueueClient client) Build(
        CapturingCallerInstrumentation? instrumentation = null,
        CapturingPendingCallRegistry? pendingCallRegistry = null)
    {
        var client = new FakeReturnQueueClient();
        var registry = new FakeReturnQueueClientRegistry(client);
        var settings = new CallReturnQueueSettings<PongMessage>("ping-reply", "stub");
        var service = new CallReturnQueueBackgroundService<PongMessage>(
            registry,
            settings,
            pendingCallRegistry ?? new CapturingPendingCallRegistry(resolves: true),
            instrumentation ?? new CapturingCallerInstrumentation(),
            NullLogger<CallReturnQueueBackgroundService<PongMessage>>.Instance);

        return (service, client.Processor, client);
    }

    private record PongMessage(string Echo);

    private sealed class FakeReturnQueueClient : IMessageBusClient
    {
        public FakeReturnQueueProcessor Processor { get; } = new();
        public string MessagingSystem => "stub";

        public Task<IMessageBusProcessor<TMessage>> CreateProcessor<TMessage>(string queueName, MessageBusProcessorOptions options)
        {
            Processor.SetQueueName(queueName);
            return Task.FromResult<IMessageBusProcessor<TMessage>>((IMessageBusProcessor<TMessage>)(object)Processor);
        }

        public Task<IMessageBusSender> CreateMessageBusSender(string queueName) => throw new NotSupportedException();
        public Task<IMessageBusReceiver> CreateReceiver(string queueName) => throw new NotSupportedException();
        public Task<IExecutionLaneProcessor> CreateExecutionLaneProcessor(string queueName, string laneFilter, MessageBusProcessorOptions options) => throw new NotSupportedException();
        public Task<IDeadLetterReceiver> CreateDeadLetterReceiver(string queueName) => throw new NotSupportedException();
        public Task<long> GetDeadLetterMessageCount(string queueName, CancellationToken cancellationToken = default) => Task.FromResult(0L);
    }

    private sealed class FakeReturnQueueProcessor : IMessageBusProcessor<PongMessage>
    {
        private readonly TaskCompletionSource<Task> _startedTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<Task> Started => _startedTcs.Task;
        public string? QueueName { get; private set; }

        public event Func<IReceivedMessage<PongMessage>, CancellationToken, Task>? ProcessMessage;
        public event Func<ErrorDetails, Task>? ProcessError;

        public void SetQueueName(string queueName) => QueueName = queueName;

        public Task StartProcessingMessages(CancellationToken cancellationToken = default)
        {
            _startedTcs.SetResult(Task.CompletedTask);
            return Task.CompletedTask;
        }

        public Task StopProcessing(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public Task FireProcessMessage(IReceivedMessage<PongMessage> message) =>
            ProcessMessage?.Invoke(message, CancellationToken.None) ?? Task.CompletedTask;
    }

    private sealed class FakeReturnQueueClientRegistry(FakeReturnQueueClient client) : IMessageBusClientRegistry
    {
        public string PrimaryProviderName => "stub";
        public IMessageBusClient GetClient(string providerName) => client;
        public bool IsRegistered(string providerName) => true;
        public IReadOnlyList<RegisteredTransport> GetAll() => [new RegisteredTransport("stub", true, null)];
        public void Register(string providerName, IMessageBusClient messageBusClient, bool? createTopologyOverride) { }
    }

    private sealed class CapturingCallerInstrumentation : ICallerInstrumentation
    {
        public List<(string ReplyQueueName, string MessagingSystem, ActivityContext ParentContext, string MessageId, string CorrelationId)> ResponseStarted { get; } = [];

        public Activity? StartCalling(string callQueueName, string messagingSystem, string messageId, string correlationId) => null;

        public void RecordSucceeded(string callQueueName, double durationMs) { }

        public void RecordFailed(string callQueueName) { }

        public Activity? StartReceivingResponse(string replyQueueName, string messagingSystem, ActivityContext parentContext, string messageId, string correlationId)
        {
            ResponseStarted.Add((replyQueueName, messagingSystem, parentContext, messageId, correlationId));
            return null;
        }
    }

    private sealed class CapturingPendingCallRegistry(bool resolves) : IPendingCallRegistry<PongMessage>
    {
        public string? ResolvedCorrelationId { get; private set; }
        public PongMessage? ResolvedBody { get; private set; }

        public Task<PongMessage> Register(string correlationId, CancellationToken cancellationToken) =>
            Task.FromResult(new PongMessage("fake"));

        public bool TryResolve(string correlationId, PongMessage body)
        {
            ResolvedCorrelationId = correlationId;
            ResolvedBody = body;
            return resolves;
        }
    }
}
