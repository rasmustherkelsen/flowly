using Flowly.MessageInfrastructure;
using Flowly.MessageInfrastructure.HandlerSteps;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.Tests;

public class HandlerStepRegistrationExtensionsTests
{
    public class AddHandlerStep
    {
        [Fact]
        public void RegistersStepAsScoped()
        {
            var flowlyBuilder = new StubFlowlyBuilder(new ServiceCollection());

            flowlyBuilder.AddHandlerStep<AuditStep>();

            var descriptor = flowlyBuilder.Services.Single(s => s.ServiceType == typeof(AuditStep));
            Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
        }

        [Fact]
        public void RegistersRegistrationSingletonWithDerivedFilterType()
        {
            var flowlyBuilder = new StubFlowlyBuilder(new ServiceCollection());

            flowlyBuilder.AddHandlerStep<AuditStep>();

            var descriptor = flowlyBuilder.Services.Single(s => s.ServiceType == typeof(HandlerStepRegistration));
            Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
            var registration = Assert.IsType<HandlerStepRegistration>(descriptor.ImplementationInstance);
            Assert.Equal(typeof(AuditStep), registration.StepType);
            Assert.Equal(typeof(IEnableAuditLog), registration.FilterType);
        }

        [Fact]
        public void WithNonGenericStep_UsesObjectAsFilterType()
        {
            var flowlyBuilder = new StubFlowlyBuilder(new ServiceCollection());

            flowlyBuilder.AddHandlerStep<EveryMessageStep>();

            var registration = Assert.IsType<HandlerStepRegistration>(flowlyBuilder.Services.Single(s => s.ServiceType == typeof(HandlerStepRegistration)).ImplementationInstance);
            Assert.Equal(typeof(object), registration.FilterType);
        }

        [Fact]
        public void KeepsRegistrationOrder()
        {
            var flowlyBuilder = new StubFlowlyBuilder(new ServiceCollection());

            flowlyBuilder.AddHandlerStep<EveryMessageStep>().AddHandlerStep<AuditStep>();

            using var serviceProvider = flowlyBuilder.Services.BuildServiceProvider();
            var stepTypes = serviceProvider.GetServices<HandlerStepRegistration>().Select(registration => registration.StepType);
            Assert.Equal([typeof(EveryMessageStep), typeof(AuditStep)], stepTypes);
        }

        [Fact]
        public void WithTypeWithoutHandlerStepBase_ThrowsArgumentException()
        {
            var flowlyBuilder = new StubFlowlyBuilder(new ServiceCollection());

            Assert.Throws<ArgumentException>(() => flowlyBuilder.AddHandlerStep<NotDerivedStep>());
        }

        [Fact]
        public void WithTypeWithoutHandlerStepBase_RegistersNothing()
        {
            var flowlyBuilder = new StubFlowlyBuilder(new ServiceCollection());

            Assert.ThrowsAny<ArgumentException>(() => flowlyBuilder.AddHandlerStep<NotDerivedStep>());

            Assert.Empty(flowlyBuilder.Services);
        }

        [Fact]
        public void ReturnsSameBuilder()
        {
            var flowlyBuilder = new StubFlowlyBuilder(new ServiceCollection());

            var returnedBuilder = flowlyBuilder.AddHandlerStep<AuditStep>();

            Assert.Same(flowlyBuilder, returnedBuilder);
        }
    }

    private interface IEnableAuditLog;

    private sealed class AuditStep : HandlerStep<IEnableAuditLog>;

    private sealed class EveryMessageStep : HandlerStep;

    private sealed class NotDerivedStep : IHandlerStep;

    private sealed class StubFlowlyBuilder(IServiceCollection services) : IFlowlyBuilder
    {
        public IServiceCollection Services => services;
        public IConfiguration Configuration => new ConfigurationBuilder().Build();
        public ITopologyNameResolver TopologyNameResolver => new KebabCaseTopologyNameResolver();
    }
}
