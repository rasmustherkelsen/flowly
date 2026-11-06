using Flowly.MessageInfrastructure;
using Flowly.MessageInfrastructure.SendSteps;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.Tests;

public class SendStepRegistrationExtensionsTests
{
    public class AddSendStep
    {
        [Fact]
        public void RegistersStepAsScoped()
        {
            var flowlyBuilder = new StubFlowlyBuilder(new ServiceCollection());

            flowlyBuilder.AddSendStep<AuthorizationStep>();

            var descriptor = flowlyBuilder.Services.Single(s => s.ServiceType == typeof(AuthorizationStep));
            Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
        }

        [Fact]
        public void RegistersRegistrationSingletonWithDerivedFilterType()
        {
            var flowlyBuilder = new StubFlowlyBuilder(new ServiceCollection());

            flowlyBuilder.AddSendStep<AuthorizationStep>();

            var descriptor = flowlyBuilder.Services.Single(s => s.ServiceType == typeof(SendStepRegistration));
            Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
            var registration = Assert.IsType<SendStepRegistration>(descriptor.ImplementationInstance);
            Assert.Equal(typeof(AuthorizationStep), registration.StepType);
            Assert.Equal(typeof(IRequiresAuthorization), registration.FilterType);
        }

        [Fact]
        public void WithNonGenericStep_UsesObjectAsFilterType()
        {
            var flowlyBuilder = new StubFlowlyBuilder(new ServiceCollection());

            flowlyBuilder.AddSendStep<EveryMessageStep>();

            var registration = Assert.IsType<SendStepRegistration>(flowlyBuilder.Services.Single(s => s.ServiceType == typeof(SendStepRegistration)).ImplementationInstance);
            Assert.Equal(typeof(object), registration.FilterType);
        }

        [Fact]
        public void KeepsRegistrationOrder()
        {
            var flowlyBuilder = new StubFlowlyBuilder(new ServiceCollection());

            flowlyBuilder.AddSendStep<EveryMessageStep>().AddSendStep<AuthorizationStep>();

            using var serviceProvider = flowlyBuilder.Services.BuildServiceProvider();
            var stepTypes = serviceProvider.GetServices<SendStepRegistration>().Select(registration => registration.StepType);
            Assert.Equal([typeof(EveryMessageStep), typeof(AuthorizationStep)], stepTypes);
        }

        [Fact]
        public void WithTypeWithoutSendStepBase_ThrowsArgumentException()
        {
            var flowlyBuilder = new StubFlowlyBuilder(new ServiceCollection());

            Assert.Throws<ArgumentException>(() => flowlyBuilder.AddSendStep<NotDerivedStep>());
        }

        [Fact]
        public void WithTypeWithoutSendStepBase_RegistersNothing()
        {
            var flowlyBuilder = new StubFlowlyBuilder(new ServiceCollection());

            Assert.ThrowsAny<ArgumentException>(() => flowlyBuilder.AddSendStep<NotDerivedStep>());

            Assert.Empty(flowlyBuilder.Services);
        }

        [Fact]
        public void ReturnsSameBuilder()
        {
            var flowlyBuilder = new StubFlowlyBuilder(new ServiceCollection());

            var returnedBuilder = flowlyBuilder.AddSendStep<AuthorizationStep>();

            Assert.Same(flowlyBuilder, returnedBuilder);
        }
    }

    private interface IRequiresAuthorization;

    private sealed class AuthorizationStep : SendStep<IRequiresAuthorization>;

    private sealed class EveryMessageStep : SendStep;

    private sealed class NotDerivedStep : ISendStep;

    private sealed class StubFlowlyBuilder(IServiceCollection services) : IFlowlyBuilder
    {
        public IServiceCollection Services => services;
        public IConfiguration Configuration => new ConfigurationBuilder().Build();
        public ITopologyNameResolver TopologyNameResolver => new KebabCaseTopologyNameResolver();
    }
}
