using Flowly.MessageInfrastructure.Events;
using Flowly.MessageInfrastructure.Events.BackgroundServices;
using Flowly.MessageInfrastructure.Events.Telemetry;
using Flowly.Tests.MessageInfrastructure.BackgroundServices;
using Flowly.Transport;
using Microsoft.Extensions.Logging.Abstractions;

namespace Flowly.Tests.MessageInfrastructure.Events.BackgroundServices;

public class EventHandlerBackgroundServiceTests
{
    private static (EventHandlerBackgroundService<TestEvent, RecordingEventHandler> backgroundService,
        FakeEventCapableMessageBusClient client,
        RecordingEventHandler handler)
        Build(int maxRetries = 0, int retryDelaySeconds = 0)
    {
        var handler = new RecordingEventHandler();
        var client = new FakeEventCapableMessageBusClient();
        var registry = new FakeMessageBusClientRegistry(client);
        var settings = new EventHandlerSettings<TestEvent, RecordingEventHandler>(
            TopicName: "order-placed",
            SubscriptionName: "audit",
            ProviderName: "azure-service-bus",
            HandlerName: "RecordingEventHandler",
            MaxConcurrentCalls: 1,
            MaxRetries: maxRetries,
            RetryDelaySeconds: retryDelaySeconds);

        var scopeFactory = new FakeServiceScopeFactory<RecordingEventHandler>(handler);
        var backgroundService = new EventHandlerBackgroundService<TestEvent, RecordingEventHandler>(
            registry,
            scopeFactory,
            settings,
            NullLogger<EventHandlerBackgroundService<TestEvent, RecordingEventHandler>>.Instance,
            new NullEventHandlerInstrumentation());

        return (backgroundService, client, handler);
    }

    public class Execute
    {
        [Fact]
        public async Task CreatesEventProcessorWithTopicAndSubscriptionFromSettings()
        {
            var (backgroundService, client, _) = Build();

            await backgroundService.StartAsync(CancellationToken.None);
            var processor = await client.ProcessorCreated;
            await processor.Started;
            await backgroundService.StopAsync(CancellationToken.None);

            Assert.Equal("order-placed", processor.TopicName);
            Assert.Equal("audit", processor.SubscriptionName);
        }

        [Fact]
        public async Task CreatesEventProcessorWithPeekLockMode()
        {
            var (backgroundService, client, _) = Build();

            await backgroundService.StartAsync(CancellationToken.None);
            var processor = await client.ProcessorCreated;
            await processor.Started;
            await backgroundService.StopAsync(CancellationToken.None);

            Assert.Equal(MessageBusReceiveMode.PeekLock, processor.Options.ReceiveMode);
        }

        [Fact]
        public async Task StartsProcessingMessages()
        {
            var (backgroundService, client, _) = Build();

            await backgroundService.StartAsync(CancellationToken.None);
            var processor = await client.ProcessorCreated;
            await processor.Started;
            await backgroundService.StopAsync(CancellationToken.None);

            Assert.True(processor.StartProcessingWasCalled);
        }

        [Fact]
        public async Task WithNonEventCapableClient_ExecuteTaskFaultsWithInvalidOperationException()
        {
            var notEventCapableClient = new NonEventCapableClient();
            var registry = new FakeMessageBusClientRegistry(notEventCapableClient);
            var settings = new EventHandlerSettings<TestEvent, RecordingEventHandler>(
                "order-placed", "audit", "azure-service-bus", "RecordingEventHandler");
            var scopeFactory = new FakeServiceScopeFactory<RecordingEventHandler>(new RecordingEventHandler());
            var backgroundService = new EventHandlerBackgroundService<TestEvent, RecordingEventHandler>(
                registry,
                scopeFactory,
                settings,
                NullLogger<EventHandlerBackgroundService<TestEvent, RecordingEventHandler>>.Instance,
                new NullEventHandlerInstrumentation());

            await backgroundService.StartAsync(CancellationToken.None);

            await Assert.ThrowsAsync<InvalidOperationException>(() => backgroundService.ExecuteTask!);
        }
    }

    public class OnProcessMessage
    {
        [Fact]
        public async Task InvokesHandlerWithEventContextContainingBody()
        {
            var (backgroundService, client, handler) = Build();
            await backgroundService.StartAsync(CancellationToken.None);
            var processor = await client.ProcessorCreated;
            await processor.Started;

            var @event = new TestEvent("order-1");
            await processor.FireProcessMessage(new FakeReceivedMessage<TestEvent>(@event));

            await backgroundService.StopAsync(CancellationToken.None);

            Assert.NotNull(handler.ReceivedContext);
            Assert.Equal(@event, handler.ReceivedContext!.Event);
        }

        [Fact]
        public async Task PassesMessageIdAndCorrelationIdToContext()
        {
            var (backgroundService, client, handler) = Build();
            await backgroundService.StartAsync(CancellationToken.None);
            var processor = await client.ProcessorCreated;
            await processor.Started;

            var properties = new MessageProperties("msg-1", "corr-1");
            await processor.FireProcessMessage(new FakeReceivedMessage<TestEvent>(new TestEvent("e"), properties));

            await backgroundService.StopAsync(CancellationToken.None);

            Assert.Equal("msg-1", handler.ReceivedContext!.MessageId);
            Assert.Equal("corr-1", handler.ReceivedContext.CorrelationId);
        }

        [Fact]
        public async Task OnSuccessfulHandling_CompletesMessage()
        {
            var (backgroundService, client, _) = Build();
            await backgroundService.StartAsync(CancellationToken.None);
            var processor = await client.ProcessorCreated;
            await processor.Started;

            var receivedMessage = new FakeReceivedMessage<TestEvent>(new TestEvent("ok"));
            await processor.FireProcessMessage(receivedMessage);

            await backgroundService.StopAsync(CancellationToken.None);

            Assert.True(receivedMessage.Completed);
        }
    }

    public class OnProcessMessageWithCorruptBody
    {
        [Fact]
        public async Task DeadLettersMessage()
        {
            var (backgroundService, client, _) = Build();
            await backgroundService.StartAsync(CancellationToken.None);
            var processor = await client.ProcessorCreated;
            await processor.Started;

            var poisonMessage = new ThrowingReceivedMessage<TestEvent>();
            await processor.FireProcessMessage(poisonMessage);

            await backgroundService.StopAsync(CancellationToken.None);

            Assert.True(poisonMessage.DeadLettered);
        }

        [Fact]
        public async Task DoesNotInvokeHandler()
        {
            var (backgroundService, client, handler) = Build();
            await backgroundService.StartAsync(CancellationToken.None);
            var processor = await client.ProcessorCreated;
            await processor.Started;

            await processor.FireProcessMessage(new ThrowingReceivedMessage<TestEvent>());

            await backgroundService.StopAsync(CancellationToken.None);

            Assert.Null(handler.ReceivedContext);
        }
    }

    public class OnHandlerThrowsBelowMaxRetries
    {
        [Fact]
        public async Task RepublishesMessageViaRetrySender()
        {
            var (backgroundService, client, handler) = Build(maxRetries: 3);
            handler.ThrowOnNext = new InvalidOperationException("boom");
            await backgroundService.StartAsync(CancellationToken.None);
            var processor = await client.ProcessorCreated;
            await processor.Started;

            var receivedMessage = new FakeReceivedMessage<TestEvent>(new TestEvent("e"), new MessageProperties("m", "c", null, RetryCount: 0));
            await processor.FireProcessMessage(receivedMessage);

            await backgroundService.StopAsync(CancellationToken.None);

            Assert.Single(client.RetrySenders);
            Assert.Single(client.RetrySenders[0].SentMessages);
        }

        [Fact]
        public async Task IncrementsRetryCountOnRepublishedMessage()
        {
            var (backgroundService, client, handler) = Build(maxRetries: 3);
            handler.ThrowOnNext = new InvalidOperationException("boom");
            await backgroundService.StartAsync(CancellationToken.None);
            var processor = await client.ProcessorCreated;
            await processor.Started;

            var receivedMessage = new FakeReceivedMessage<TestEvent>(new TestEvent("e"), new MessageProperties("m", "c", null, RetryCount: 1));
            await processor.FireProcessMessage(receivedMessage);

            await backgroundService.StopAsync(CancellationToken.None);

            var (_, props) = client.RetrySenders[0].SentMessages[0];
            Assert.Equal(2, props.RetryCount);
        }

        [Fact]
        public async Task SetsScheduledEnqueueTimeUsingRetryDelay()
        {
            var (backgroundService, client, handler) = Build(maxRetries: 3, retryDelaySeconds: 30);
            handler.ThrowOnNext = new InvalidOperationException("boom");
            await backgroundService.StartAsync(CancellationToken.None);
            var processor = await client.ProcessorCreated;
            await processor.Started;

            var beforeCall = DateTimeOffset.UtcNow;
            await processor.FireProcessMessage(new FakeReceivedMessage<TestEvent>(new TestEvent("e")));

            await backgroundService.StopAsync(CancellationToken.None);

            var (_, props) = client.RetrySenders[0].SentMessages[0];
            Assert.NotNull(props.ScheduledEnqueueTime);
            Assert.True(props.ScheduledEnqueueTime!.Value >= beforeCall + TimeSpan.FromSeconds(30) - TimeSpan.FromSeconds(1));
        }

        [Fact]
        public async Task CompletesOriginalMessage()
        {
            var (backgroundService, client, handler) = Build(maxRetries: 3);
            handler.ThrowOnNext = new InvalidOperationException("boom");
            await backgroundService.StartAsync(CancellationToken.None);
            var processor = await client.ProcessorCreated;
            await processor.Started;

            var receivedMessage = new FakeReceivedMessage<TestEvent>(new TestEvent("e"));
            await processor.FireProcessMessage(receivedMessage);

            await backgroundService.StopAsync(CancellationToken.None);

            Assert.True(receivedMessage.Completed);
            Assert.False(receivedMessage.DeadLettered);
        }
    }

    public class OnHandlerThrowsAtMaxRetries
    {
        [Fact]
        public async Task DeadLettersMessage()
        {
            var (backgroundService, client, handler) = Build(maxRetries: 2);
            handler.ThrowOnNext = new InvalidOperationException("final");
            await backgroundService.StartAsync(CancellationToken.None);
            var processor = await client.ProcessorCreated;
            await processor.Started;

            var receivedMessage = new FakeReceivedMessage<TestEvent>(new TestEvent("e"), new MessageProperties("m", "c", null, RetryCount: 2));
            await processor.FireProcessMessage(receivedMessage);

            await backgroundService.StopAsync(CancellationToken.None);

            Assert.True(receivedMessage.DeadLettered);
        }

        [Fact]
        public async Task DeadLetterReasonIsExceptionMessage()
        {
            var (backgroundService, client, handler) = Build(maxRetries: 2);
            handler.ThrowOnNext = new InvalidOperationException("final");
            await backgroundService.StartAsync(CancellationToken.None);
            var processor = await client.ProcessorCreated;
            await processor.Started;

            var receivedMessage = new FakeReceivedMessage<TestEvent>(new TestEvent("e"), new MessageProperties("m", "c", null, RetryCount: 2));
            await processor.FireProcessMessage(receivedMessage);

            await backgroundService.StopAsync(CancellationToken.None);

            Assert.Equal("final", receivedMessage.DeadLetterReason);
        }

        [Fact]
        public async Task DoesNotRepublishForRetry()
        {
            var (backgroundService, client, handler) = Build(maxRetries: 2);
            handler.ThrowOnNext = new InvalidOperationException("final");
            await backgroundService.StartAsync(CancellationToken.None);
            var processor = await client.ProcessorCreated;
            await processor.Started;

            await processor.FireProcessMessage(new FakeReceivedMessage<TestEvent>(new TestEvent("e"), new MessageProperties("m", "c", null, RetryCount: 2)));

            await backgroundService.StopAsync(CancellationToken.None);

            Assert.Empty(client.RetrySenders);
        }
    }

    public class OnProcessError
    {
        [Fact]
        public async Task DoesNotThrow()
        {
            var (backgroundService, client, _) = Build();

            await backgroundService.StartAsync(CancellationToken.None);
            var processor = await client.ProcessorCreated;
            await processor.Started;

            var exception = await Record.ExceptionAsync(() =>
                processor.FireProcessError(new ErrorDetails(new Exception("boom"), "endpoint")));

            await backgroundService.StopAsync(CancellationToken.None);

            Assert.Null(exception);
        }
    }

    private record TestEvent(string Value);

    private class RecordingEventHandler : EventHandlerBase<TestEvent>
    {
        public IEventContext<TestEvent>? ReceivedContext { get; private set; }
        public Exception? ThrowOnNext { get; set; }

        public override Task Handle(IEventContext<TestEvent> eventContext, CancellationToken cancellationToken)
        {
            ReceivedContext = eventContext;

            if (ThrowOnNext is { } toThrow)
            {
                ThrowOnNext = null;
                throw toThrow;
            }

            return Task.CompletedTask;
        }
    }

    private class NonEventCapableClient : IMessageBusClient
    {
        public string MessagingSystem => "fake";

        public Task<IMessageBusProcessor<TMessage>> CreateProcessor<TMessage>(string queueName, MessageBusProcessorOptions options) => throw new NotImplementedException();

        public Task<IMessageBusReceiver> CreateReceiver(string queueName) => throw new NotImplementedException();

        public Task<IExecutionLaneProcessor> CreateExecutionLaneProcessor(string queueName, string laneFilter, MessageBusProcessorOptions options) => throw new NotImplementedException();

        public Task<IMessageBusSender> CreateMessageBusSender(string queueName) => throw new NotImplementedException();

        public Task<IDeadLetterReceiver> CreateDeadLetterReceiver(string queueName) => throw new NotImplementedException();

        public Task<long> GetDeadLetterMessageCount(string queueName, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private class FakeEventCapableMessageBusClient : IMessageBusClient, IEventCapableMessageBusClient
    {
        private readonly TaskCompletionSource<FakeEventBusProcessor<TestEvent>> _processorCreated = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<FakeEventBusProcessor<TestEvent>> ProcessorCreated => _processorCreated.Task;

        public string MessagingSystem => "fake";

        public List<RecordingEventBusSender> RetrySenders { get; } = new();

        public Task<IMessageBusProcessor<TMessage>> CreateProcessor<TMessage>(string queueName, MessageBusProcessorOptions options) => throw new NotImplementedException();

        public Task<IMessageBusReceiver> CreateReceiver(string queueName) => throw new NotImplementedException();

        public Task<IExecutionLaneProcessor> CreateExecutionLaneProcessor(string queueName, string laneFilter, MessageBusProcessorOptions options) => throw new NotImplementedException();

        public Task<IMessageBusSender> CreateMessageBusSender(string queueName) => throw new NotImplementedException();

        public Task<IDeadLetterReceiver> CreateDeadLetterReceiver(string queueName) => throw new NotImplementedException();

        public Task<long> GetDeadLetterMessageCount(string queueName, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<IMessageBusSender> CreateEventPublisher(string topicName) => throw new NotImplementedException();

        public Task<IMessageBusProcessor<TEvent>> CreateEventProcessor<TEvent>(string topicName, string subscriptionName, MessageBusProcessorOptions options)
        {
            var processor = new FakeEventBusProcessor<TEvent>(topicName, subscriptionName, options);
            _processorCreated.SetResult((FakeEventBusProcessor<TestEvent>)(object)processor);
            return Task.FromResult<IMessageBusProcessor<TEvent>>(processor);
        }

        public Task<IMessageBusSender> CreateEventRetrySender(string topicName, string subscriptionName)
        {
            var sender = new RecordingEventBusSender(topicName, subscriptionName);
            RetrySenders.Add(sender);
            return Task.FromResult<IMessageBusSender>(sender);
        }

        public Task<IDeadLetterReceiver> CreateEventSubscriptionDeadLetterReceiver(string topicName, string subscriptionName) => throw new NotImplementedException();

        public Task<long> GetEventSubscriptionDeadLetterMessageCount(string topicName, string subscriptionName, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private class FakeEventBusProcessor<TMessage>(string topicName, string subscriptionName, MessageBusProcessorOptions options) : IMessageBusProcessor<TMessage>
    {
        private readonly TaskCompletionSource _startedTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public string TopicName { get; } = topicName;
        public string SubscriptionName { get; } = subscriptionName;
        public MessageBusProcessorOptions Options { get; } = options;
        public bool StartProcessingWasCalled { get; private set; }
        public Task Started => _startedTcs.Task;

        public event Func<IReceivedMessage<TMessage>, CancellationToken, Task>? ProcessMessage;
        public event Func<ErrorDetails, Task>? ProcessError;

        public Task StartProcessingMessages(CancellationToken cancellationToken = default)
        {
            StartProcessingWasCalled = true;
            _startedTcs.SetResult();
            return Task.CompletedTask;
        }

        public Task StopProcessing(CancellationToken cancellationToken) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public Task FireProcessMessage(IReceivedMessage<TMessage> message)
        {
            return ProcessMessage?.Invoke(message, CancellationToken.None) ?? Task.CompletedTask;
        }

        public Task FireProcessError(ErrorDetails error)
        {
            return ProcessError?.Invoke(error) ?? Task.CompletedTask;
        }
    }

    private class RecordingEventBusSender(string topicName, string subscriptionName) : IMessageBusSender
    {
        public string TopicName { get; } = topicName;
        public string SubscriptionName { get; } = subscriptionName;
        public List<(object Body, MessageProperties Properties)> SentMessages { get; } = new();

        public Task SendMessage<TMessage>(TMessage message, MessageProperties messageProperties, CancellationToken cancellationToken = default)
        {
            SentMessages.Add((message!, messageProperties));
            return Task.CompletedTask;
        }

        public Task SendEmptyMessage(MessageProperties messageProperties, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task SendRawMessage(string rawBody, IReadOnlyDictionary<string, object> applicationProperties, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
