using Flowly.MessageInfrastructure.MessageHandlingStrategies;
using Flowly.MessageInfrastructure.Model;
using Flowly.MessageInfrastructure.Registration;
using Flowly.Transport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Flowly.Tests.MessageInfrastructure.MessageHandlingStrategies;

public class CallMessageHandlingStrategyTests
{
    public class HandleMessage
    {
        [Fact]
        public async Task SendsHandlerResponseToReplyToQueue()
        {
            var sender = new CapturingSender();
            var (strategy, handler) = CreateStrategy(sender);
            var receivedMessage = new FakeCallReceivedMessage(
                new PingMessage("test-payload"),
                new MessageProperties("msg-1", "corr-1", ReplyTo: "reply.sender"));

            var services = new ServiceCollection();
            services.AddScoped<CallHandler<PingMessage, PongMessage>>(_ => handler);
            using var scope = services.BuildServiceProvider().CreateScope();

            await strategy.HandleMessage(receivedMessage, scope.ServiceProvider, CancellationToken.None);

            Assert.NotNull(sender.SentMessage);
            Assert.IsType<PongMessage>(sender.SentMessage);
            Assert.Equal("echo: test-payload", ((PongMessage)sender.SentMessage!).Echo);
        }

        [Fact]
        public async Task PreservesCorrelationIdInResponse()
        {
            var sender = new CapturingSender();
            var (strategy, handler) = CreateStrategy(sender);
            var receivedMessage = new FakeCallReceivedMessage(
                new PingMessage("payload"),
                new MessageProperties("msg-1", "my-correlation-id", ReplyTo: "reply.sender"));

            var services = new ServiceCollection();
            services.AddScoped<CallHandler<PingMessage, PongMessage>>(_ => handler);
            using var scope = services.BuildServiceProvider().CreateScope();

            await strategy.HandleMessage(receivedMessage, scope.ServiceProvider, CancellationToken.None);

            Assert.Equal("my-correlation-id", sender.SentProperties?.CorrelationId);
        }

        [Fact]
        public async Task SendsToTheReplyToQueueFromMessageProperties()
        {
            var sender = new CapturingSender();
            var fakeSenderFactory = new FakeSenderFactory("reply.sender", sender);
            var (strategy, handler) = CreateStrategy(sender, fakeSenderFactory);
            var receivedMessage = new FakeCallReceivedMessage(
                new PingMessage("payload"),
                new MessageProperties("msg-1", "corr-1", ReplyTo: "reply.sender"));

            var services = new ServiceCollection();
            services.AddScoped<CallHandler<PingMessage, PongMessage>>(_ => handler);
            using var scope = services.BuildServiceProvider().CreateScope();

            await strategy.HandleMessage(receivedMessage, scope.ServiceProvider, CancellationToken.None);

            Assert.Equal("reply.sender", fakeSenderFactory.RequestedQueueName);
        }

        [Fact]
        public async Task WithMissingReplyTo_Throws()
        {
            var sender = new CapturingSender();
            var (strategy, _) = CreateStrategy(sender);
            var receivedMessage = new FakeCallReceivedMessage(
                new PingMessage("payload"),
                new MessageProperties("msg-1", "corr-1"));

            var services = new ServiceCollection();
            using var scope = services.BuildServiceProvider().CreateScope();

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => strategy.HandleMessage(receivedMessage, scope.ServiceProvider, CancellationToken.None));
        }
    }

    public class OnRetriesExhausted
    {
        [Fact]
        public async Task DeadLettersMessageWithExceptionMessage()
        {
            var (strategy, _) = CreateStrategy(new CapturingSender());
            var receivedMessage = new FakeCallReceivedMessage(
                new PingMessage("p"),
                new MessageProperties("m", "c", ReplyTo: "r"));
            var serviceProvider = new ServiceCollection().BuildServiceProvider();

            await strategy.OnRetriesExhausted(
                receivedMessage,
                new InvalidOperationException("handler blew up"),
                serviceProvider,
                CancellationToken.None);

            Assert.True(receivedMessage.WasDeadLettered);
            Assert.Equal("handler blew up", receivedMessage.DeadLetterReason);
        }
    }

    public class OnMessageHandlingError
    {
        [Fact]
        public async Task LogsExceptionMessageAtErrorLevel()
        {
            var (strategy, _) = CreateStrategy(new CapturingSender());
            var logger = new FakeLogger();
            var serviceProvider = new ServiceCollection().BuildServiceProvider();

            await strategy.OnMessageHandlingError(
                logger,
                serviceProvider,
                new ErrorDetails(new InvalidOperationException("broker error"), "endpoint"));

            Assert.Single(logger.Messages);
            Assert.Equal(LogLevel.Error, logger.Messages[0].Level);
            Assert.Equal("broker error", logger.Messages[0].Message);
        }
    }

    private static (CallMessageHandlingStrategy<PingMessage, PongMessage> Strategy, EchoCallHandler Handler) CreateStrategy(
        CapturingSender sender,
        FakeSenderFactory? senderFactory = null)
    {
        senderFactory ??= new FakeSenderFactory(null, sender);
        var fakeClient = new FakeMessageBusClient(senderFactory);
        var registry = new FakeClientRegistry(fakeClient);
        var handlerSettings = new HandlerSettings<PingMessage>("ping", "stub", "EchoCallHandler", false, 1, 0, 0, 1, TimeSpan.FromSeconds(30));
        var handler = new EchoCallHandler();
        var strategy = new CallMessageHandlingStrategy<PingMessage, PongMessage>(registry, handlerSettings);
        return (strategy, handler);
    }

    private record PingMessage(string Payload) : IReturns<PongMessage>;
    private record PongMessage(string Echo);

    private sealed class EchoCallHandler : CallHandler<PingMessage, PongMessage>
    {
        protected override Task<PongMessage> Handle(IMessageContext<PingMessage> messageContext)
            => Task.FromResult(new PongMessage($"echo: {messageContext.Message.Payload}"));
    }

    private sealed class CapturingSender : IMessageBusSender
    {
        public object? SentMessage { get; private set; }
        public MessageProperties? SentProperties { get; private set; }

        public Task SendMessage<TMessage>(TMessage message, MessageProperties messageProperties, CancellationToken cancellationToken = default)
        {
            SentMessage = message;
            SentProperties = messageProperties;
            return Task.CompletedTask;
        }

        public Task SendEmptyMessage(MessageProperties messageProperties, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SendRawMessage(string rawBody, IReadOnlyDictionary<string, object> applicationProperties, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeSenderFactory(string? _, CapturingSender sender)
    {
        public string? RequestedQueueName { get; private set; }

        public Task<IMessageBusSender> CreateSender(string queueName)
        {
            RequestedQueueName = queueName;
            return Task.FromResult<IMessageBusSender>(sender);
        }
    }

    private sealed class FakeMessageBusClient(FakeSenderFactory senderFactory) : IMessageBusClient
    {
        public string MessagingSystem => "stub";
        public Task<IMessageBusSender> CreateMessageBusSender(string queueName) => senderFactory.CreateSender(queueName);
        public Task<IMessageBusReceiver> CreateReceiver(string queueName) => throw new NotSupportedException();
        public Task<IMessageBusProcessor<TMessage>> CreateProcessor<TMessage>(string queueName, MessageBusProcessorOptions options) => throw new NotSupportedException();
        public Task<IExecutionLaneProcessor> CreateExecutionLaneProcessor(string queueName, string laneFilter, MessageBusProcessorOptions options) => throw new NotSupportedException();
        public Task<IDeadLetterReceiver> CreateDeadLetterReceiver(string queueName) => throw new NotSupportedException();
        public Task<long> GetDeadLetterMessageCount(string queueName, CancellationToken cancellationToken = default) => Task.FromResult(0L);
    }

    private sealed class FakeClientRegistry(IMessageBusClient client) : IMessageBusClientRegistry
    {
        public string PrimaryProviderName => "stub";
        public IMessageBusClient GetClient(string providerName) => client;
        public bool IsRegistered(string providerName) => true;
        public IReadOnlyList<RegisteredTransport> GetAll() => [new RegisteredTransport("stub", true, null)];
        public void Register(string providerName, IMessageBusClient messageBusClient, bool? createTopologyOverride) { }
    }

    private sealed class FakeCallReceivedMessage(PingMessage body, MessageProperties properties) : IReceivedMessage<PingMessage>
    {
        public bool WasDeadLettered { get; private set; }
        public string? DeadLetterReason { get; private set; }
        public PingMessage Body => body;
        public MessageProperties Properties => properties;
        public Task Complete(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeadLetter(string? reason = null, CancellationToken cancellationToken = default)
        {
            WasDeadLettered = true;
            DeadLetterReason = reason;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeLogger : ILogger
    {
        public List<(LogLevel Level, string Message)> Messages { get; } = [];
        IDisposable? ILogger.BeginScope<TState>(TState state) => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Messages.Add((logLevel, formatter(state, exception)));
    }
}
