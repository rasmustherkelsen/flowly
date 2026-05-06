using Flowly.MessageInfrastructure.MessageHandlingStrategies;
using Flowly.Transport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Flowly.Tests.MessageInfrastructure.MessageHandlingStrategies;

public class StandardMessageHandlingStrategyTests
{
    public class HandleMessage
    {
        [Fact]
        public async Task DispatchesMessageBodyToHandler()
        {
            var handler = new TestOrderHandler();
            var services = new ServiceCollection();
            services.AddScoped<MessageHandler<OrderMessage>>(_ => handler);
            using var scope = services.BuildServiceProvider().CreateScope();

            var receivedMessage = new FakeReceivedMessage<OrderMessage>(new OrderMessage("order-1"));
            var standardMessageHandlingStrategy = new StandardMessageHandlingStrategy<OrderMessage>();

            await standardMessageHandlingStrategy.HandleMessage(receivedMessage, scope.ServiceProvider, CancellationToken.None);

            Assert.Equal("order-1", handler.ReceivedOrderId);
        }

        [Fact]
        public async Task WithNoHandlerRegistered_Throws()
        {
            var serviceProvider = new ServiceCollection().BuildServiceProvider();
            var receivedMessage = new FakeReceivedMessage<OrderMessage>(new OrderMessage("order-1"));
            var standardMessageHandlingStrategy = new StandardMessageHandlingStrategy<OrderMessage>();

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => standardMessageHandlingStrategy.HandleMessage(receivedMessage, serviceProvider, CancellationToken.None));
        }

        [Fact]
        public async Task WithThrowingHandler_PropagatesException()
        {
            var services = new ServiceCollection();
            services.AddScoped<MessageHandler<OrderMessage>, ThrowingOrderHandler>();
            using var scope = services.BuildServiceProvider().CreateScope();

            var receivedMessage = new FakeReceivedMessage<OrderMessage>(new OrderMessage("order-1"));
            var standardMessageHandlingStrategy = new StandardMessageHandlingStrategy<OrderMessage>();

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => standardMessageHandlingStrategy.HandleMessage(receivedMessage, scope.ServiceProvider, CancellationToken.None));
        }

        [Fact]
        public async Task WithCancelledToken_Throws()
        {
            var services = new ServiceCollection();
            services.AddScoped<MessageHandler<OrderMessage>, TestOrderHandler>();
            using var scope = services.BuildServiceProvider().CreateScope();

            var receivedMessage = new FakeReceivedMessage<OrderMessage>(new OrderMessage("order-1"));
            var standardMessageHandlingStrategy = new StandardMessageHandlingStrategy<OrderMessage>();

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(
                () => standardMessageHandlingStrategy.HandleMessage(receivedMessage, scope.ServiceProvider, cts.Token));
        }
    }

    public class OnRetriesExhausted
    {
        [Fact]
        public async Task DeadLettersMessageWithExceptionMessage()
        {
            var receivedMessage = new FakeReceivedMessage<OrderMessage>(new OrderMessage("order-1"));
            var serviceProvider = new ServiceCollection().BuildServiceProvider();
            var standardMessageHandlingStrategy = new StandardMessageHandlingStrategy<OrderMessage>();

            await standardMessageHandlingStrategy.OnRetriesExhausted(receivedMessage, new InvalidOperationException("test error"), serviceProvider, CancellationToken.None);

            Assert.True(receivedMessage.WasDeadLetterCalled);
            Assert.Equal("test error", receivedMessage.DeadLetterReason);
        }

        [Fact]
        public async Task PropagatesCancellationToken()
        {
            var receivedMessage = new FakeReceivedMessage<OrderMessage>(new OrderMessage("order-1"));
            var serviceProvider = new ServiceCollection().BuildServiceProvider();
            var standardMessageHandlingStrategy = new StandardMessageHandlingStrategy<OrderMessage>();
            using var cts = new CancellationTokenSource();

            await standardMessageHandlingStrategy.OnRetriesExhausted(receivedMessage, new InvalidOperationException(), serviceProvider, cts.Token);

            Assert.Equal(cts.Token, receivedMessage.DeadLetterCancellationToken);
        }
    }

    public class OnMessageHandlingError
    {
        [Fact]
        public async Task LogsExceptionMessageAtErrorLevel()
        {
            var logger = new FakeLogger();
            var serviceProvider = new ServiceCollection().BuildServiceProvider();
            var errorDetails = new ErrorDetails(new InvalidOperationException("broker error"), "test-endpoint");
            var standardMessageHandlingStrategy = new StandardMessageHandlingStrategy<OrderMessage>();

            await standardMessageHandlingStrategy.OnMessageHandlingError(logger, serviceProvider, errorDetails);

            Assert.Single(logger.Messages);
            Assert.Equal(LogLevel.Error, logger.Messages[0].Level);
            Assert.Equal("broker error", logger.Messages[0].Message);
        }
    }

    private sealed class TestOrderHandler : MessageHandler<OrderMessage>
    {
        public string? ReceivedOrderId { get; private set; }

        public override Task Handle(IMessageContext<OrderMessage> messageContext)
        {
            ReceivedOrderId = messageContext.Message?.OrderId;
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingOrderHandler : MessageHandler<OrderMessage>
    {
        public override Task Handle(IMessageContext<OrderMessage> messageContext)
            => throw new InvalidOperationException("handler failed");
    }

    private record OrderMessage(string OrderId);

    private sealed class FakeLogger : ILogger
    {
        public List<(LogLevel Level, string Message)> Messages { get; } = [];

        IDisposable? ILogger.BeginScope<TState>(TState state) => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Messages.Add((logLevel, formatter(state, exception)));
    }

    private sealed class FakeReceivedMessage<T>(T body) : IReceivedMessage<T>
    {
        public bool WasDeadLetterCalled { get; private set; }
        public string? DeadLetterReason { get; private set; }
        public CancellationToken DeadLetterCancellationToken { get; private set; }

        public T Body => body;

        public MessageProperties Properties => MessageProperties.Empty;

        public Task Complete(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task DeadLetter(string? reason = null, CancellationToken cancellationToken = default)
        {
            WasDeadLetterCalled = true;
            DeadLetterReason = reason;
            DeadLetterCancellationToken = cancellationToken;
            return Task.CompletedTask;
        }
    }
}
