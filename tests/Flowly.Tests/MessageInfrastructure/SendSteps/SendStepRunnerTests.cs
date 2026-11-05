using Flowly.MessageInfrastructure.SendSteps;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.Tests.MessageInfrastructure.SendSteps;

public class SendStepRunnerTests
{
    private static ServiceProvider CreateServiceProvider(List<string> log, params Type[] stepTypes)
    {
        var services = new ServiceCollection();
        services.AddSingleton(log);
        services.AddScoped<ScopedDependency>();

        foreach (var stepType in stepTypes)
        {
            services.AddScoped(stepType);
        }

        return services.BuildServiceProvider();
    }

    private static SendStepRunner CreateRunner(IServiceProvider serviceProvider, params Type[] stepTypes)
        => new(
            stepTypes.Select(stepType => new SendStepRegistration(stepType, SendStepRegistration.ResolveFilterType(stepType))),
            serviceProvider.GetRequiredService<IServiceScopeFactory>());

    private static Task RunSend(SendStepRunner sendStepRunner, object message, List<string> log)
        => sendStepRunner.Run(
            message,
            CancellationToken.None,
            () =>
            {
                log.Add("send");

                return Task.CompletedTask;
            });

    public class Run
    {
        [Fact]
        public async Task FirstRegisteredStepIsOutermost()
        {
            var log = new List<string>();
            using var serviceProvider = CreateServiceProvider(log, typeof(OuterStep), typeof(InnerStep));
            var sendStepRunner = CreateRunner(serviceProvider, typeof(OuterStep), typeof(InnerStep));

            await RunSend(sendStepRunner, new PlainMessage(), log);

            Assert.Equal(["OuterStep:before", "InnerStep:before", "send", "InnerStep:after", "OuterStep:after"], log);
        }

        [Fact]
        public async Task WhenStepThrows_ExceptionPropagatesUnchangedAndSendIsNotCalled()
        {
            var log = new List<string>();
            using var serviceProvider = CreateServiceProvider(log, typeof(RejectingStep), typeof(InnerStep));
            var sendStepRunner = CreateRunner(serviceProvider, typeof(RejectingStep), typeof(InnerStep));

            var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => RunSend(sendStepRunner, new PlainMessage(), log));

            Assert.Equal("rejected", exception.Message);
            Assert.Empty(log);
        }

        [Fact]
        public async Task WhenSendThrows_ExceptionPropagatesAndAfterHooksDoNotRun()
        {
            var log = new List<string>();
            using var serviceProvider = CreateServiceProvider(log, typeof(OuterStep));
            var sendStepRunner = CreateRunner(serviceProvider, typeof(OuterStep));

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => sendStepRunner.Run(
                new PlainMessage(),
                CancellationToken.None,
                () => throw new InvalidOperationException("send failed")));

            Assert.Equal("send failed", exception.Message);
            Assert.Equal(["OuterStep:before"], log);
        }

        [Fact]
        public async Task StepReturningWithoutNext_DoesNotCallSendOrInnerSteps()
        {
            var log = new List<string>();
            using var serviceProvider = CreateServiceProvider(log, typeof(OuterStep), typeof(ShortCircuitStep), typeof(InnerStep));
            var sendStepRunner = CreateRunner(serviceProvider, typeof(OuterStep), typeof(ShortCircuitStep), typeof(InnerStep));

            await RunSend(sendStepRunner, new PlainMessage(), log);

            Assert.Equal(["OuterStep:before", "ShortCircuitStep", "OuterStep:after"], log);
        }

        [Fact]
        public async Task StepFilteredByInterface_RunsForMessageImplementingInterface()
        {
            var log = new List<string>();
            using var serviceProvider = CreateServiceProvider(log, typeof(AuthorizationStep));
            var sendStepRunner = CreateRunner(serviceProvider, typeof(AuthorizationStep));

            await RunSend(sendStepRunner, new AuthorizedMessage(), log);

            Assert.Equal(["AuthorizationStep:before", "send", "AuthorizationStep:after"], log);
        }

        [Fact]
        public async Task StepFilteredByBaseClass_RunsForDerivedMessage()
        {
            var log = new List<string>();
            using var serviceProvider = CreateServiceProvider(log, typeof(BaseMessageStep));
            var sendStepRunner = CreateRunner(serviceProvider, typeof(BaseMessageStep));

            await RunSend(sendStepRunner, new AuthorizedMessage(), log);

            Assert.Equal(["BaseMessageStep:before", "send", "BaseMessageStep:after"], log);
        }

        [Fact]
        public async Task StepFilteredByObject_RunsForEveryMessage()
        {
            var log = new List<string>();
            using var serviceProvider = CreateServiceProvider(log, typeof(OuterStep));
            var sendStepRunner = CreateRunner(serviceProvider, typeof(OuterStep));

            await RunSend(sendStepRunner, new PlainMessage(), log);
            await RunSend(sendStepRunner, new AuthorizedMessage(), log);

            Assert.Equal(["OuterStep:before", "send", "OuterStep:after", "OuterStep:before", "send", "OuterStep:after"], log);
        }

        [Fact]
        public async Task NonMatchingStep_DoesNotRun()
        {
            var log = new List<string>();
            using var serviceProvider = CreateServiceProvider(log, typeof(AuthorizationStep), typeof(OuterStep));
            var sendStepRunner = CreateRunner(serviceProvider, typeof(AuthorizationStep), typeof(OuterStep));

            await RunSend(sendStepRunner, new PlainMessage(), log);

            Assert.Equal(["OuterStep:before", "send", "OuterStep:after"], log);
        }

        [Fact]
        public async Task MatchesOnRuntimeTypeOfMessage()
        {
            var log = new List<string>();
            using var serviceProvider = CreateServiceProvider(log, typeof(AuthorizationStep));
            var sendStepRunner = CreateRunner(serviceProvider, typeof(AuthorizationStep));
            object message = new AuthorizedMessage();

            await RunSend(sendStepRunner, message, log);

            Assert.Equal(["AuthorizationStep:before", "send", "AuthorizationStep:after"], log);
        }

        [Fact]
        public async Task DifferentMessageTypes_GetTheirOwnMatchingSteps()
        {
            var log = new List<string>();
            using var serviceProvider = CreateServiceProvider(log, typeof(AuthorizationStep));
            var sendStepRunner = CreateRunner(serviceProvider, typeof(AuthorizationStep));

            await RunSend(sendStepRunner, new PlainMessage(), log);
            await RunSend(sendStepRunner, new AuthorizedMessage(), log);
            await RunSend(sendStepRunner, new PlainMessage(), log);

            Assert.Equal(["send", "AuthorizationStep:before", "send", "AuthorizationStep:after", "send"], log);
        }

        [Fact]
        public async Task WithNoMatchingSteps_CreatesNoScope()
        {
            var log = new List<string>();
            var sendStepRunner = new SendStepRunner(
                [new SendStepRegistration(typeof(AuthorizationStep), typeof(IRequiresAuthorization))],
                new ThrowingServiceScopeFactory());

            await RunSend(sendStepRunner, new PlainMessage(), log);

            Assert.Equal(["send"], log);
        }

        [Fact]
        public async Task WithNoRegistrations_CreatesNoScope()
        {
            var log = new List<string>();
            var sendStepRunner = new SendStepRunner([], new ThrowingServiceScopeFactory());

            await RunSend(sendStepRunner, new PlainMessage(), log);

            Assert.Equal(["send"], log);
        }

        [Fact]
        public async Task None_CallsSendDirectly()
        {
            var log = new List<string>();

            await RunSend(SendStepRunner.None, new PlainMessage(), log);

            Assert.Equal(["send"], log);
        }

        [Fact]
        public async Task CreatesOneScopePerSendAndDisposesItAfterwards()
        {
            var log = new List<string>();
            var scopeFactory = new CountingServiceScopeFactory(CreateServiceProvider(log, typeof(OuterStep), typeof(InnerStep)));
            var sendStepRunner = new SendStepRunner(
                [
                    new SendStepRegistration(typeof(OuterStep), typeof(object)),
                    new SendStepRegistration(typeof(InnerStep), typeof(object))
                ],
                scopeFactory);

            await RunSend(sendStepRunner, new PlainMessage(), log);

            Assert.Equal(1, scopeFactory.CreatedScopes);
            Assert.Equal(1, scopeFactory.DisposedScopes);
            await RunSend(sendStepRunner, new PlainMessage(), log);
            Assert.Equal(2, scopeFactory.CreatedScopes);
            Assert.Equal(2, scopeFactory.DisposedScopes);
            scopeFactory.DisposeProvider();
        }

        [Fact]
        public async Task DisposesScopeWhenStepThrows()
        {
            var log = new List<string>();
            var scopeFactory = new CountingServiceScopeFactory(CreateServiceProvider(log, typeof(RejectingStep)));
            var sendStepRunner = new SendStepRunner([new SendStepRegistration(typeof(RejectingStep), typeof(object))], scopeFactory);

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => RunSend(sendStepRunner, new PlainMessage(), log));

            Assert.Equal(1, scopeFactory.DisposedScopes);
            scopeFactory.DisposeProvider();
        }

        [Fact]
        public async Task ScopedDependencyResolvedByTwoStepsInOneSend_IsTheSameInstanceAndDiffersBetweenSends()
        {
            var log = new List<string>();
            using var serviceProvider = CreateServiceProvider(log, typeof(FirstScopeIdentityStep), typeof(SecondScopeIdentityStep));
            var sendStepRunner = CreateRunner(serviceProvider, typeof(FirstScopeIdentityStep), typeof(SecondScopeIdentityStep));

            await RunSend(sendStepRunner, new PlainMessage(), log);
            await RunSend(sendStepRunner, new PlainMessage(), log);

            var identities = log.Where(entry => entry != "send").ToArray();
            Assert.Equal(4, identities.Length);
            Assert.Equal(identities[0], identities[1]);
            Assert.Equal(identities[2], identities[3]);
            Assert.NotEqual(identities[1], identities[2]);
        }

        [Fact]
        public async Task StepsResolveScopedDependencyDifferentlyFromTheRootProvidersScope()
        {
            var log = new List<string>();
            using var serviceProvider = CreateServiceProvider(log, typeof(FirstScopeIdentityStep));
            using var callerScope = serviceProvider.CreateScope();
            var sendStepRunner = CreateRunner(serviceProvider, typeof(FirstScopeIdentityStep));

            await RunSend(sendStepRunner, new PlainMessage(), log);

            var callerIdentity = $"scoped-{callerScope.ServiceProvider.GetRequiredService<ScopedDependency>().GetHashCode()}";
            Assert.DoesNotContain(callerIdentity, log);
        }

        [Fact]
        public async Task FlowlySystemMessage_BypassesStepsAndScope()
        {
            var log = new List<string>();
            var sendStepRunner = new SendStepRunner([new SendStepRegistration(typeof(OuterStep), typeof(object))], new ThrowingServiceScopeFactory());

            await RunSend(sendStepRunner, new InternalMessage(), log);

            Assert.Equal(["send"], log);
        }

        [Fact]
        public async Task PassesMessageAndCancellationTokenToTheContext()
        {
            var contexts = new List<SendStepContext<IRequiresAuthorization>>();
            var message = new AuthorizedMessage();
            using var cancellationTokenSource = new CancellationTokenSource();
            var services = new ServiceCollection();
            services.AddSingleton(contexts);
            services.AddScoped<ContextCapturingStep>();
            using var serviceProvider = services.BuildServiceProvider();
            var sendStepRunner = CreateRunner(serviceProvider, typeof(ContextCapturingStep));

            await sendStepRunner.Run(message, cancellationTokenSource.Token, () => Task.CompletedTask);

            var sendStepContext = Assert.Single(contexts);
            Assert.Same(message, sendStepContext.Message);
            Assert.Equal(cancellationTokenSource.Token, sendStepContext.CancellationToken);
        }

        [Fact]
        public async Task ActivityCurrentFlowsThroughTheFreshScope()
        {
            var log = new List<string>();
            var observedActivities = new List<System.Diagnostics.Activity?>();
            var services = new ServiceCollection();
            services.AddSingleton(observedActivities);
            services.AddScoped<ActivityCapturingStep>();
            using var serviceProvider = services.BuildServiceProvider();
            var sendStepRunner = CreateRunner(serviceProvider, typeof(ActivityCapturingStep));
            using var activity = new System.Diagnostics.Activity("caller").Start();

            await RunSend(sendStepRunner, new PlainMessage(), log);

            Assert.Same(activity, Assert.Single(observedActivities));
        }
    }

    private interface IRequiresAuthorization;

    private class BaseMessage;

    private sealed class PlainMessage;

    private sealed class AuthorizedMessage : BaseMessage, IRequiresAuthorization;

    private sealed class InternalMessage : IFlowlySystemMessage;

    private sealed class ScopedDependency;

    private sealed class OuterStep(List<string> log) : SendStep
    {
        public override async Task Handle(SendStepContext<object> context, SendStepDelegate next)
        {
            log.Add("OuterStep:before");
            await next();
            log.Add("OuterStep:after");
        }
    }

    private sealed class InnerStep(List<string> log) : SendStep
    {
        public override async Task Handle(SendStepContext<object> context, SendStepDelegate next)
        {
            log.Add("InnerStep:before");
            await next();
            log.Add("InnerStep:after");
        }
    }

    private sealed class RejectingStep : SendStep
    {
        public override Task Handle(SendStepContext<object> context, SendStepDelegate next)
            => throw new UnauthorizedAccessException("rejected");
    }

    private sealed class ShortCircuitStep(List<string> log) : SendStep
    {
        public override Task Handle(SendStepContext<object> context, SendStepDelegate next)
        {
            log.Add("ShortCircuitStep");

            return Task.CompletedTask;
        }
    }

    private sealed class AuthorizationStep(List<string> log) : SendStep<IRequiresAuthorization>
    {
        public override async Task Handle(SendStepContext<IRequiresAuthorization> context, SendStepDelegate next)
        {
            log.Add("AuthorizationStep:before");
            await next();
            log.Add("AuthorizationStep:after");
        }
    }

    private sealed class BaseMessageStep(List<string> log) : SendStep<BaseMessage>
    {
        public override async Task Handle(SendStepContext<BaseMessage> context, SendStepDelegate next)
        {
            log.Add("BaseMessageStep:before");
            await next();
            log.Add("BaseMessageStep:after");
        }
    }

    private sealed class FirstScopeIdentityStep(List<string> log, ScopedDependency scopedDependency) : SendStep
    {
        public override Task Handle(SendStepContext<object> context, SendStepDelegate next)
        {
            log.Add($"scoped-{scopedDependency.GetHashCode()}");

            return next();
        }
    }

    private sealed class SecondScopeIdentityStep(List<string> log, ScopedDependency scopedDependency) : SendStep
    {
        public override Task Handle(SendStepContext<object> context, SendStepDelegate next)
        {
            log.Add($"scoped-{scopedDependency.GetHashCode()}");

            return next();
        }
    }

    private sealed class ContextCapturingStep(List<SendStepContext<IRequiresAuthorization>> contexts) : SendStep<IRequiresAuthorization>
    {
        public override Task Handle(SendStepContext<IRequiresAuthorization> context, SendStepDelegate next)
        {
            contexts.Add(context);

            return next();
        }
    }

    private sealed class ActivityCapturingStep(List<System.Diagnostics.Activity?> observedActivities) : SendStep
    {
        public override Task Handle(SendStepContext<object> context, SendStepDelegate next)
        {
            observedActivities.Add(System.Diagnostics.Activity.Current);

            return next();
        }
    }

    private sealed class ThrowingServiceScopeFactory : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => throw new InvalidOperationException("No scope may be created.");
    }

    private sealed class CountingServiceScopeFactory(ServiceProvider serviceProvider) : IServiceScopeFactory
    {
        public int CreatedScopes { get; private set; }

        public int DisposedScopes { get; private set; }

        public IServiceScope CreateScope()
        {
            CreatedScopes++;

            return new CountingServiceScope(serviceProvider.CreateScope(), () => DisposedScopes++);
        }

        public void DisposeProvider() => serviceProvider.Dispose();
    }

    private sealed class CountingServiceScope(IServiceScope innerScope, Action onDispose) : IServiceScope
    {
        public IServiceProvider ServiceProvider => innerScope.ServiceProvider;

        public void Dispose()
        {
            innerScope.Dispose();
            onDispose();
        }
    }
}
