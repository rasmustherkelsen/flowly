using Flowly.MessageInfrastructure.HandlerSteps;
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
            var standardMessageHandlingStrategy = new StandardMessageHandlingStrategy<OrderMessage>(HandlerStepRunner.None);

            await standardMessageHandlingStrategy.HandleMessage(receivedMessage, scope.ServiceProvider, CancellationToken.None);

            Assert.Equal("order-1", handler.ReceivedOrderId);
        }

        [Fact]
        public async Task PassesTheDeliverysPropertiesToTheHandlerContext()
        {
            var handler = new TestOrderHandler();
            var services = new ServiceCollection();
            services.AddScoped<MessageHandler<OrderMessage>>(_ => handler);
            using var scope = services.BuildServiceProvider().CreateScope();
            var properties = new MessageProperties("m", "c") { CustomProperties = new Dictionary<string, string> { ["tenant"] = "contoso" } };
            var receivedMessage = new FakeReceivedMessage<OrderMessage>(new OrderMessage("order-1"), properties);
            var standardMessageHandlingStrategy = new StandardMessageHandlingStrategy<OrderMessage>(HandlerStepRunner.None);

            await standardMessageHandlingStrategy.HandleMessage(receivedMessage, scope.ServiceProvider, CancellationToken.None);

            Assert.Equal("contoso", handler.ReceivedProperties?.CustomProperties["tenant"]);
        }

        [Fact]
        public async Task WithNoHandlerRegistered_Throws()
        {
            var serviceProvider = new ServiceCollection().BuildServiceProvider();
            var receivedMessage = new FakeReceivedMessage<OrderMessage>(new OrderMessage("order-1"));
            var standardMessageHandlingStrategy = new StandardMessageHandlingStrategy<OrderMessage>(HandlerStepRunner.None);

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
            var standardMessageHandlingStrategy = new StandardMessageHandlingStrategy<OrderMessage>(HandlerStepRunner.None);

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
            var standardMessageHandlingStrategy = new StandardMessageHandlingStrategy<OrderMessage>(HandlerStepRunner.None);

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(
                () => standardMessageHandlingStrategy.HandleMessage(receivedMessage, scope.ServiceProvider, cts.Token));
        }

        [Fact]
        public async Task WithRegisteredStep_RunsStepAroundHandler()
        {
            var log = new List<string>();
            using var scope = CreateScope(log, typeof(LoggingStep));
            var standardMessageHandlingStrategy = new StandardMessageHandlingStrategy<OrderMessage>(CreateRunner(typeof(LoggingStep)));

            await standardMessageHandlingStrategy.HandleMessage(new FakeReceivedMessage<OrderMessage>(new OrderMessage("order-1")), scope.ServiceProvider, CancellationToken.None);

            Assert.Equal(["step:before", "handler", "step:after"], log);
        }

        [Fact]
        public async Task WithStepNotCallingNext_DoesNotInvokeHandler()
        {
            var log = new List<string>();
            using var scope = CreateScope(log, typeof(SkippingStep));
            var standardMessageHandlingStrategy = new StandardMessageHandlingStrategy<OrderMessage>(CreateRunner(typeof(SkippingStep)));

            await standardMessageHandlingStrategy.HandleMessage(new FakeReceivedMessage<OrderMessage>(new OrderMessage("order-1")), scope.ServiceProvider, CancellationToken.None);

            Assert.Equal(["step:skipped"], log);
        }

        [Fact]
        public async Task WithThrowingStep_PropagatesExceptionWithoutInvokingHandler()
        {
            var log = new List<string>();
            using var scope = CreateScope(log, typeof(ThrowingStep));
            var standardMessageHandlingStrategy = new StandardMessageHandlingStrategy<OrderMessage>(CreateRunner(typeof(ThrowingStep)));

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => standardMessageHandlingStrategy.HandleMessage(new FakeReceivedMessage<OrderMessage>(new OrderMessage("order-1")), scope.ServiceProvider, CancellationToken.None));

            Assert.Empty(log);
        }

        [Fact]
        public async Task WithStepForOtherMessageType_DoesNotRunStep()
        {
            var log = new List<string>();
            using var scope = CreateScope(log, typeof(InvoiceOnlyStep));
            var standardMessageHandlingStrategy = new StandardMessageHandlingStrategy<OrderMessage>(CreateRunner(typeof(InvoiceOnlyStep)));

            await standardMessageHandlingStrategy.HandleMessage(new FakeReceivedMessage<OrderMessage>(new OrderMessage("order-1")), scope.ServiceProvider, CancellationToken.None);

            Assert.Equal(["handler"], log);
        }

        [Fact]
        public async Task StepAndHandlerShareScopedService()
        {
            var log = new List<string>();
            using var scope = CreateScope(log, typeof(ScopedStateStep));
            var standardMessageHandlingStrategy = new StandardMessageHandlingStrategy<OrderMessage>(CreateRunner(typeof(ScopedStateStep)));

            await standardMessageHandlingStrategy.HandleMessage(new FakeReceivedMessage<OrderMessage>(new OrderMessage("order-1")), scope.ServiceProvider, CancellationToken.None);

            Assert.Equal(["handler:state=from-step"], log);
        }

        [Fact]
        public async Task PassesHandlerTypeAndPropertiesToStep()
        {
            var log = new List<string>();
            using var scope = CreateScope(log, typeof(ContextLoggingStep));
            var standardMessageHandlingStrategy = new StandardMessageHandlingStrategy<OrderMessage>(CreateRunner(typeof(ContextLoggingStep)));
            var properties = MessageProperties.Empty with { MessageId = "message-1" };

            await standardMessageHandlingStrategy.HandleMessage(new FakeReceivedMessage<OrderMessage>(new OrderMessage("order-1"), properties), scope.ServiceProvider, CancellationToken.None);

            Assert.Equal([$"step:{nameof(LoggingOrderHandler)}:message-1", "handler"], log);
        }
    }

    public class OnRetriesExhausted
    {
        [Fact]
        public async Task DeadLettersMessageWithExceptionMessage()
        {
            var receivedMessage = new FakeReceivedMessage<OrderMessage>(new OrderMessage("order-1"));
            var serviceProvider = new ServiceCollection().BuildServiceProvider();
            var standardMessageHandlingStrategy = new StandardMessageHandlingStrategy<OrderMessage>(HandlerStepRunner.None);

            await standardMessageHandlingStrategy.OnRetriesExhausted(receivedMessage, new InvalidOperationException("test error"), serviceProvider, CancellationToken.None);

            Assert.True(receivedMessage.WasDeadLetterCalled);
            Assert.Equal("test error", receivedMessage.DeadLetterReason);
        }

        [Fact]
        public async Task PropagatesCancellationToken()
        {
            var receivedMessage = new FakeReceivedMessage<OrderMessage>(new OrderMessage("order-1"));
            var serviceProvider = new ServiceCollection().BuildServiceProvider();
            var standardMessageHandlingStrategy = new StandardMessageHandlingStrategy<OrderMessage>(HandlerStepRunner.None);
            using var cts = new CancellationTokenSource();

            await standardMessageHandlingStrategy.OnRetriesExhausted(receivedMessage, new InvalidOperationException(), serviceProvider, cts.Token);

            Assert.Equal(cts.Token, receivedMessage.DeadLetterCancellationToken);
        }
    }

    public class OnMessageHandlingError
    {
        [Fact]
        public async Task LogsExceptionAtErrorLevel()
        {
            var logger = new FakeLogger();
            var serviceProvider = new ServiceCollection().BuildServiceProvider();
            var exception = new InvalidOperationException("broker error");
            var errorDetails = new ErrorDetails(exception, "test-endpoint");
            var standardMessageHandlingStrategy = new StandardMessageHandlingStrategy<OrderMessage>(HandlerStepRunner.None);

            await standardMessageHandlingStrategy.OnMessageHandlingError(logger, serviceProvider, errorDetails);

            Assert.Single(logger.Messages);
            Assert.Equal(LogLevel.Error, logger.Messages[0].Level);
            Assert.Equal("Message processor error", logger.Messages[0].Message);
            Assert.Same(exception, logger.Messages[0].Exception);
        }
    }

    private static IServiceScope CreateScope(List<string> log, params Type[] stepTypes)
    {
        var services = new ServiceCollection();
        services.AddSingleton(log);
        services.AddScoped<ScopedState>();
        services.AddScoped<MessageHandler<OrderMessage>, LoggingOrderHandler>();

        foreach (var stepType in stepTypes)
        {
            services.AddScoped(stepType);
        }

        return services.BuildServiceProvider().CreateScope();
    }

    private static HandlerStepRunner CreateRunner(params Type[] stepTypes)
        => new(stepTypes.Select(stepType => new HandlerStepRegistration(stepType, HandlerStepRegistration.ResolveFilterType(stepType))));

    private sealed class TestOrderHandler : MessageHandler<OrderMessage>
    {
        public string? ReceivedOrderId { get; private set; }
        public MessageProperties? ReceivedProperties { get; private set; }

        public override Task Handle(IMessageContext<OrderMessage> messageContext)
        {
            ReceivedOrderId = messageContext.Message?.OrderId;
            ReceivedProperties = messageContext.Properties;
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingOrderHandler : MessageHandler<OrderMessage>
    {
        public override Task Handle(IMessageContext<OrderMessage> messageContext)
            => throw new InvalidOperationException("handler failed");
    }

    private sealed class ScopedState
    {
        public string? Value { get; set; }
    }

    private sealed class LoggingOrderHandler(List<string> log, ScopedState scopedState) : MessageHandler<OrderMessage>
    {
        public override Task Handle(IMessageContext<OrderMessage> messageContext)
        {
            log.Add(scopedState.Value is null ? "handler" : $"handler:state={scopedState.Value}");
            return Task.CompletedTask;
        }
    }

    private sealed class LoggingStep(List<string> log) : HandlerStep<OrderMessage>
    {
        protected override Task OnBeforeHandlerExecution(HandlerStepContext<OrderMessage> context)
        {
            log.Add("step:before");
            return Task.CompletedTask;
        }

        protected override Task OnAfterHandlerExecution(HandlerStepContext<OrderMessage> context)
        {
            log.Add("step:after");
            return Task.CompletedTask;
        }
    }

    private sealed class SkippingStep(List<string> log) : HandlerStep<OrderMessage>
    {
        public override Task Handle(HandlerStepContext<OrderMessage> context, HandlerStepDelegate next)
        {
            log.Add("step:skipped");
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingStep : HandlerStep<OrderMessage>
    {
        public override Task Handle(HandlerStepContext<OrderMessage> context, HandlerStepDelegate next)
            => throw new InvalidOperationException("step failed");
    }

    private sealed class InvoiceOnlyStep(List<string> log) : HandlerStep<InvoiceMessage>
    {
        public override Task Handle(HandlerStepContext<InvoiceMessage> context, HandlerStepDelegate next)
        {
            log.Add("invoice-step");
            return next();
        }
    }

    private sealed class ScopedStateStep(ScopedState scopedState) : HandlerStep<OrderMessage>
    {
        protected override Task OnBeforeHandlerExecution(HandlerStepContext<OrderMessage> context)
        {
            scopedState.Value = "from-step";
            return Task.CompletedTask;
        }
    }

    private sealed class ContextLoggingStep(List<string> log) : HandlerStep<OrderMessage>
    {
        protected override Task OnBeforeHandlerExecution(HandlerStepContext<OrderMessage> context)
        {
            log.Add($"step:{context.HandlerType.Name}:{context.Properties.MessageId}");
            return Task.CompletedTask;
        }
    }

    private record InvoiceMessage(string InvoiceId);

    private record OrderMessage(string OrderId);

    private sealed class FakeLogger : ILogger
    {
        public List<(LogLevel Level, string Message, Exception? Exception)> Messages { get; } = [];

        IDisposable? ILogger.BeginScope<TState>(TState state) => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Messages.Add((logLevel, formatter(state, exception), exception));
    }

    private sealed class FakeReceivedMessage<T>(T body, MessageProperties? properties = null) : IReceivedMessage<T>
    {
        public bool WasDeadLetterCalled { get; private set; }
        public string? DeadLetterReason { get; private set; }
        public CancellationToken DeadLetterCancellationToken { get; private set; }

        public T Body => body;

        public MessageProperties Properties => properties ?? MessageProperties.Empty;

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
