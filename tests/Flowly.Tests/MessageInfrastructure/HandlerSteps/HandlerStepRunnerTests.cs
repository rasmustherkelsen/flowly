using Flowly.MessageInfrastructure.HandlerSteps;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.Tests.MessageInfrastructure.HandlerSteps;

public class HandlerStepRunnerTests
{
    private static ServiceProvider CreateServiceProvider(List<string> log, params Type[] stepTypes)
    {
        var services = new ServiceCollection();
        services.AddSingleton(log);

        foreach (var stepType in stepTypes)
        {
            services.AddScoped(stepType);
        }

        return services.BuildServiceProvider();
    }

    private static HandlerStepRunner CreateRunner(params Type[] stepTypes)
        => new(stepTypes.Select(stepType => new HandlerStepRegistration(stepType, HandlerStepRegistration.ResolveFilterType(stepType))));

    private static Task RunHandler(HandlerStepRunner handlerStepRunner, object message, IServiceProvider serviceProvider, List<string> log)
        => handlerStepRunner.Run(
            message,
            MessageProperties.Empty,
            typeof(OrderHandler),
            serviceProvider,
            CancellationToken.None,
            () =>
            {
                log.Add("handler");
                return Task.CompletedTask;
            });

    public class Run
    {
        [Fact]
        public async Task FirstRegisteredStepIsOutermost()
        {
            var log = new List<string>();
            var handlerStepRunner = CreateRunner(typeof(OuterStep), typeof(InnerStep));
            using var serviceProvider = CreateServiceProvider(log, typeof(OuterStep), typeof(InnerStep));

            await RunHandler(handlerStepRunner, new PlainMessage(), serviceProvider, log);

            Assert.Equal(["OuterStep:before", "InnerStep:before", "handler", "InnerStep:after", "OuterStep:after"], log);
        }

        [Fact]
        public async Task WithSingleStep_RunsBeforeHandlerAndAfter()
        {
            var log = new List<string>();
            var handlerStepRunner = CreateRunner(typeof(OuterStep));
            using var serviceProvider = CreateServiceProvider(log, typeof(OuterStep));

            await RunHandler(handlerStepRunner, new PlainMessage(), serviceProvider, log);

            Assert.Equal(["OuterStep:before", "handler", "OuterStep:after"], log);
        }

        [Fact]
        public async Task WhenHandlerThrows_AfterHooksDoNotRun()
        {
            var log = new List<string>();
            var handlerStepRunner = CreateRunner(typeof(OuterStep), typeof(InnerStep));
            using var serviceProvider = CreateServiceProvider(log, typeof(OuterStep), typeof(InnerStep));

            await Assert.ThrowsAsync<InvalidOperationException>(() => handlerStepRunner.Run(
                new PlainMessage(),
                MessageProperties.Empty,
                typeof(OrderHandler),
                serviceProvider,
                CancellationToken.None,
                () => throw new InvalidOperationException("handler failed")));

            Assert.Equal(["OuterStep:before", "InnerStep:before"], log);
        }

        [Fact]
        public async Task WhenHandlerThrows_ExceptionPropagatesThroughOuterStepsAndTheirFinallyBlocksRun()
        {
            var log = new List<string>();
            var handlerStepRunner = CreateRunner(typeof(FinallyStep), typeof(InnerStep));
            using var serviceProvider = CreateServiceProvider(log, typeof(FinallyStep), typeof(InnerStep));

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => handlerStepRunner.Run(
                new PlainMessage(),
                MessageProperties.Empty,
                typeof(OrderHandler),
                serviceProvider,
                CancellationToken.None,
                () => throw new InvalidOperationException("handler failed")));

            Assert.Equal("handler failed", exception.Message);
            Assert.Equal(["InnerStep:before", "FinallyStep:finally"], log);
        }

        [Fact]
        public async Task WhenStepThrows_InnerStepsAndHandlerDoNotRun()
        {
            var log = new List<string>();
            var handlerStepRunner = CreateRunner(typeof(ThrowingStep), typeof(InnerStep));
            using var serviceProvider = CreateServiceProvider(log, typeof(ThrowingStep), typeof(InnerStep));

            await Assert.ThrowsAsync<InvalidOperationException>(() => RunHandler(handlerStepRunner, new PlainMessage(), serviceProvider, log));

            Assert.Empty(log);
        }

        [Fact]
        public async Task StepReturningWithoutNext_SkipsHandlerAndInnerSteps()
        {
            var log = new List<string>();
            var handlerStepRunner = CreateRunner(typeof(OuterStep), typeof(ShortCircuitStep), typeof(InnerStep));
            using var serviceProvider = CreateServiceProvider(log, typeof(OuterStep), typeof(ShortCircuitStep), typeof(InnerStep));

            await RunHandler(handlerStepRunner, new PlainMessage(), serviceProvider, log);

            Assert.Equal(["OuterStep:before", "ShortCircuitStep", "OuterStep:after"], log);
        }

        [Fact]
        public async Task StepSwallowingException_LetsRunComplete()
        {
            var log = new List<string>();
            var handlerStepRunner = CreateRunner(typeof(SwallowingStep));
            using var serviceProvider = CreateServiceProvider(log, typeof(SwallowingStep));

            await handlerStepRunner.Run(
                new PlainMessage(),
                MessageProperties.Empty,
                typeof(OrderHandler),
                serviceProvider,
                CancellationToken.None,
                () => throw new InvalidOperationException("handler failed"));

            Assert.Equal(["SwallowingStep:swallowed"], log);
        }

        [Fact]
        public async Task StepFilteredByInterface_RunsForMessageImplementingInterface()
        {
            var log = new List<string>();
            var handlerStepRunner = CreateRunner(typeof(AuditStep));
            using var serviceProvider = CreateServiceProvider(log, typeof(AuditStep));

            await RunHandler(handlerStepRunner, new AuditedMessage(), serviceProvider, log);

            Assert.Equal(["AuditStep:before", "handler", "AuditStep:after"], log);
        }

        [Fact]
        public async Task StepFilteredByBaseClass_RunsForDerivedMessage()
        {
            var log = new List<string>();
            var handlerStepRunner = CreateRunner(typeof(BaseMessageStep));
            using var serviceProvider = CreateServiceProvider(log, typeof(BaseMessageStep));

            await RunHandler(handlerStepRunner, new AuditedMessage(), serviceProvider, log);

            Assert.Equal(["BaseMessageStep:before", "handler", "BaseMessageStep:after"], log);
        }

        [Fact]
        public async Task StepFilteredByObject_RunsForEveryMessage()
        {
            var log = new List<string>();
            var handlerStepRunner = CreateRunner(typeof(OuterStep));
            using var serviceProvider = CreateServiceProvider(log, typeof(OuterStep));

            await RunHandler(handlerStepRunner, new PlainMessage(), serviceProvider, log);
            await RunHandler(handlerStepRunner, new AuditedMessage(), serviceProvider, log);

            Assert.Equal(["OuterStep:before", "handler", "OuterStep:after", "OuterStep:before", "handler", "OuterStep:after"], log);
        }

        [Fact]
        public async Task NonMatchingStep_DoesNotRun()
        {
            var log = new List<string>();
            var handlerStepRunner = CreateRunner(typeof(AuditStep), typeof(OuterStep));
            using var serviceProvider = CreateServiceProvider(log, typeof(AuditStep), typeof(OuterStep));

            await RunHandler(handlerStepRunner, new PlainMessage(), serviceProvider, log);

            Assert.Equal(["OuterStep:before", "handler", "OuterStep:after"], log);
        }

        [Fact]
        public async Task MatchesOnRuntimeTypeOfMessage()
        {
            var log = new List<string>();
            var handlerStepRunner = CreateRunner(typeof(AuditStep));
            using var serviceProvider = CreateServiceProvider(log, typeof(AuditStep));
            object message = new AuditedMessage();

            await RunHandler(handlerStepRunner, message, serviceProvider, log);

            Assert.Equal(["AuditStep:before", "handler", "AuditStep:after"], log);
        }

        [Fact]
        public async Task WithNoMatchingSteps_CallsHandlerWithoutResolvingAnything()
        {
            var log = new List<string>();
            var handlerStepRunner = CreateRunner(typeof(AuditStep));

            await RunHandler(handlerStepRunner, new PlainMessage(), new ThrowingServiceProvider(), log);

            Assert.Equal(["handler"], log);
        }

        [Fact]
        public async Task WithNoRegistrations_CallsHandlerWithoutResolvingAnything()
        {
            var log = new List<string>();

            await RunHandler(HandlerStepRunner.None, new PlainMessage(), new ThrowingServiceProvider(), log);

            Assert.Equal(["handler"], log);
        }

        [Fact]
        public async Task RepeatedRunsForTheSameMessageType_RunTheSameSteps()
        {
            var log = new List<string>();
            var handlerStepRunner = CreateRunner(typeof(AuditStep), typeof(OuterStep));
            using var serviceProvider = CreateServiceProvider(log, typeof(AuditStep), typeof(OuterStep));

            await RunHandler(handlerStepRunner, new AuditedMessage(), serviceProvider, log);
            var firstRun = log.ToArray();
            log.Clear();
            await RunHandler(handlerStepRunner, new AuditedMessage(), serviceProvider, log);

            Assert.Equal(firstRun, log);
        }

        [Fact]
        public async Task DifferentMessageTypes_GetTheirOwnMatchingSteps()
        {
            var log = new List<string>();
            var handlerStepRunner = CreateRunner(typeof(AuditStep));
            using var serviceProvider = CreateServiceProvider(log, typeof(AuditStep));

            await RunHandler(handlerStepRunner, new PlainMessage(), serviceProvider, log);
            await RunHandler(handlerStepRunner, new AuditedMessage(), serviceProvider, log);
            await RunHandler(handlerStepRunner, new PlainMessage(), serviceProvider, log);

            Assert.Equal(["handler", "AuditStep:before", "handler", "AuditStep:after", "handler"], log);
        }

        [Fact]
        public async Task ResolvesStepsFromTheSuppliedScope()
        {
            var log = new List<string>();
            var handlerStepRunner = CreateRunner(typeof(ScopeIdentityStep));
            using var rootServiceProvider = CreateServiceProvider(log, typeof(ScopeIdentityStep));
            using var firstScope = rootServiceProvider.CreateScope();
            using var secondScope = rootServiceProvider.CreateScope();

            await RunHandler(handlerStepRunner, new PlainMessage(), firstScope.ServiceProvider, log);
            await RunHandler(handlerStepRunner, new PlainMessage(), firstScope.ServiceProvider, log);
            await RunHandler(handlerStepRunner, new PlainMessage(), secondScope.ServiceProvider, log);

            var stepIdentities = log.Where(entry => entry != "handler").ToArray();
            Assert.Equal(3, stepIdentities.Length);
            Assert.Equal(stepIdentities[0], stepIdentities[1]);
            Assert.NotEqual(stepIdentities[1], stepIdentities[2]);
        }

        [Fact]
        public async Task PassesMessagePropertiesHandlerTypeAndCancellationTokenToTheContext()
        {
            var contexts = new List<HandlerStepContext<IEnableAuditLog>>();
            var message = new AuditedMessage();
            var properties = new MessageProperties("message-id", "correlation-id", RetryCount: 1);
            using var cancellationTokenSource = new CancellationTokenSource();
            var handlerStepRunner = CreateRunner(typeof(ContextCapturingStep));
            var services = new ServiceCollection();
            services.AddSingleton(contexts);
            services.AddScoped<ContextCapturingStep>();
            using var serviceProvider = services.BuildServiceProvider();

            await handlerStepRunner.Run(message, properties, typeof(OrderHandler), serviceProvider, cancellationTokenSource.Token, () => Task.CompletedTask);

            var handlerStepContext = Assert.Single(contexts);
            Assert.Same(message, handlerStepContext.Message);
            Assert.Same(properties, handlerStepContext.Properties);
            Assert.Equal(typeof(OrderHandler), handlerStepContext.HandlerType);
            Assert.Equal(cancellationTokenSource.Token, handlerStepContext.CancellationToken);
        }
    }

    private interface IEnableAuditLog;

    private class BaseMessage;

    private sealed class PlainMessage;

    private sealed class AuditedMessage : BaseMessage, IEnableAuditLog;

    private sealed class OrderHandler;

    private sealed class OuterStep(List<string> log) : HandlerStep
    {
        protected override Task OnBeforeHandlerExecution(HandlerStepContext<object> context)
        {
            log.Add("OuterStep:before");

            return Task.CompletedTask;
        }

        protected override Task OnAfterHandlerExecution(HandlerStepContext<object> context)
        {
            log.Add("OuterStep:after");

            return Task.CompletedTask;
        }
    }

    private sealed class InnerStep(List<string> log) : HandlerStep
    {
        protected override Task OnBeforeHandlerExecution(HandlerStepContext<object> context)
        {
            log.Add("InnerStep:before");

            return Task.CompletedTask;
        }

        protected override Task OnAfterHandlerExecution(HandlerStepContext<object> context)
        {
            log.Add("InnerStep:after");

            return Task.CompletedTask;
        }
    }

    private sealed class FinallyStep(List<string> log) : HandlerStep
    {
        public override async Task Handle(HandlerStepContext<object> context, HandlerStepDelegate next)
        {
            try
            {
                await next();
            }
            finally
            {
                log.Add("FinallyStep:finally");
            }
        }
    }

    private sealed class ThrowingStep : HandlerStep
    {
        public override Task Handle(HandlerStepContext<object> context, HandlerStepDelegate next)
            => throw new InvalidOperationException("step failed");
    }

    private sealed class ShortCircuitStep(List<string> log) : HandlerStep
    {
        public override Task Handle(HandlerStepContext<object> context, HandlerStepDelegate next)
        {
            log.Add("ShortCircuitStep");

            return Task.CompletedTask;
        }
    }

    private sealed class SwallowingStep(List<string> log) : HandlerStep
    {
        public override async Task Handle(HandlerStepContext<object> context, HandlerStepDelegate next)
        {
            try
            {
                await next();
            }
            catch (InvalidOperationException)
            {
                log.Add("SwallowingStep:swallowed");
            }
        }
    }

    private sealed class AuditStep(List<string> log) : HandlerStep<IEnableAuditLog>
    {
        protected override Task OnBeforeHandlerExecution(HandlerStepContext<IEnableAuditLog> context)
        {
            log.Add("AuditStep:before");

            return Task.CompletedTask;
        }

        protected override Task OnAfterHandlerExecution(HandlerStepContext<IEnableAuditLog> context)
        {
            log.Add("AuditStep:after");

            return Task.CompletedTask;
        }
    }

    private sealed class BaseMessageStep(List<string> log) : HandlerStep<BaseMessage>
    {
        protected override Task OnBeforeHandlerExecution(HandlerStepContext<BaseMessage> context)
        {
            log.Add("BaseMessageStep:before");

            return Task.CompletedTask;
        }

        protected override Task OnAfterHandlerExecution(HandlerStepContext<BaseMessage> context)
        {
            log.Add("BaseMessageStep:after");

            return Task.CompletedTask;
        }
    }

    private sealed class ScopeIdentityStep(List<string> log) : HandlerStep
    {
        protected override Task OnBeforeHandlerExecution(HandlerStepContext<object> context)
        {
            log.Add($"step-{GetHashCode()}");

            return Task.CompletedTask;
        }
    }

    private sealed class ContextCapturingStep(List<HandlerStepContext<IEnableAuditLog>> contexts) : HandlerStep<IEnableAuditLog>
    {
        protected override Task OnBeforeHandlerExecution(HandlerStepContext<IEnableAuditLog> context)
        {
            contexts.Add(context);

            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => throw new InvalidOperationException($"Nothing may be resolved, but {serviceType} was requested.");
    }
}
