using Flowly.MessageInfrastructure.HandlerSteps;

namespace Flowly.Tests;

public class HandlerStepTests
{
    private static HandlerStepContext<object> CreateContext() => new(new object(), MessageProperties.Empty, typeof(HandlerStepTests), CancellationToken.None);

    public class Handle
    {
        [Fact]
        public async Task RunsBeforeHookThenNextThenAfterHook()
        {
            var log = new List<string>();
            var hookStep = new LoggingHookStep(log);

            await hookStep.Handle(CreateContext(), () =>
            {
                log.Add("next");
                return Task.CompletedTask;
            });

            Assert.Equal(["before", "next", "after"], log);
        }

        [Fact]
        public async Task WhenNextThrows_DoesNotRunAfterHook()
        {
            var log = new List<string>();
            var hookStep = new LoggingHookStep(log);

            await Assert.ThrowsAsync<InvalidOperationException>(() => hookStep.Handle(CreateContext(), () => throw new InvalidOperationException()));

            Assert.Equal(["before"], log);
        }

        [Fact]
        public async Task WithoutOverriddenHooks_OnlyRunsNext()
        {
            var log = new List<string>();
            var passThroughStep = new PassThroughStep();

            await passThroughStep.Handle(CreateContext(), () =>
            {
                log.Add("next");
                return Task.CompletedTask;
            });

            Assert.Equal(["next"], log);
        }

        [Fact]
        public async Task PassesContextToHooks()
        {
            var log = new List<string>();
            var hookStep = new LoggingHookStep(log);
            var handlerStepContext = CreateContext();

            await hookStep.Handle(handlerStepContext, () => Task.CompletedTask);

            Assert.Same(handlerStepContext, hookStep.BeforeContext);
            Assert.Same(handlerStepContext, hookStep.AfterContext);
        }
    }

    public class Invoke
    {
        [Fact]
        public async Task BuildsTypedContextFromArguments()
        {
            var message = new OrderMessage();
            var properties = new MessageProperties("message-id", "correlation-id");
            using var cancellationTokenSource = new CancellationTokenSource();
            var capturingStep = new CapturingStep();

            await ((IHandlerStepInvoker)capturingStep).Invoke(message, properties, typeof(HandlerStepTests), cancellationTokenSource.Token, () => Task.CompletedTask);

            Assert.NotNull(capturingStep.Context);
            Assert.Same(message, capturingStep.Context.Message);
            Assert.Same(properties, capturingStep.Context.Properties);
            Assert.Equal(typeof(HandlerStepTests), capturingStep.Context.HandlerType);
            Assert.Equal(cancellationTokenSource.Token, capturingStep.Context.CancellationToken);
        }

        [Fact]
        public async Task CallsNext()
        {
            var nextCalled = false;
            var passThroughStep = new PassThroughStep();

            await ((IHandlerStepInvoker)passThroughStep).Invoke(new object(), MessageProperties.Empty, typeof(HandlerStepTests), CancellationToken.None, () =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            });

            Assert.True(nextCalled);
        }
    }

    private sealed class OrderMessage;

    private sealed class PassThroughStep : HandlerStep;

    private sealed class LoggingHookStep(List<string> log) : HandlerStep
    {
        public HandlerStepContext<object>? BeforeContext { get; private set; }

        public HandlerStepContext<object>? AfterContext { get; private set; }

        protected override Task OnBeforeHandlerExecution(HandlerStepContext<object> context)
        {
            BeforeContext = context;
            log.Add("before");

            return Task.CompletedTask;
        }

        protected override Task OnAfterHandlerExecution(HandlerStepContext<object> context)
        {
            AfterContext = context;
            log.Add("after");

            return Task.CompletedTask;
        }
    }

    private sealed class CapturingStep : HandlerStep<OrderMessage>
    {
        public HandlerStepContext<OrderMessage>? Context { get; private set; }

        public override Task Handle(HandlerStepContext<OrderMessage> context, HandlerStepDelegate next)
        {
            Context = context;

            return next();
        }
    }
}
