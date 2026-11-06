using Flowly.MessageInfrastructure.HandlerSteps;
using Flowly.MessageInfrastructure.Model;
using Flowly.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Flowly.Tests.MessageInfrastructure.HandlerSteps;

public class HandlerStepConfigurationValidatorTests
{
    public class StartAsync
    {
        [Fact]
        public async Task StepMatchingAHandler_DoesNotWarn()
        {
            var services = new ServiceCollection();
            AddHandler<AuditedOrderMessage>(services);
            AddStep<AuditStep>(services);
            var logger = new RecordingLogger<HandlerStepConfigurationValidator>();

            await Validator(services, logger).StartAsync(CancellationToken.None);

            Assert.Empty(logger.Entries);
        }

        [Fact]
        public async Task StepMatchingNoHandler_WarnsNamingStepAndFilterType()
        {
            var services = new ServiceCollection();
            AddHandler<PlainOrderMessage>(services);
            AddStep<AuditStep>(services);
            var logger = new RecordingLogger<HandlerStepConfigurationValidator>();

            await Validator(services, logger).StartAsync(CancellationToken.None);

            var entry = Assert.Single(logger.Entries);
            Assert.Equal(LogLevel.Warning, entry.Level);
            Assert.Contains(typeof(AuditStep).ToString(), entry.Message);
            Assert.Contains(typeof(IEnableAuditLog).ToString(), entry.Message);
        }

        [Fact]
        public async Task ObjectFilterWithAnyHandler_DoesNotWarn()
        {
            var services = new ServiceCollection();
            AddHandler<PlainOrderMessage>(services);
            AddStep<EveryMessageStep>(services);
            var logger = new RecordingLogger<HandlerStepConfigurationValidator>();

            await Validator(services, logger).StartAsync(CancellationToken.None);

            Assert.Empty(logger.Entries);
        }

        [Fact]
        public async Task ObjectFilterWithNoHandlers_Warns()
        {
            var services = new ServiceCollection();
            AddStep<EveryMessageStep>(services);
            var logger = new RecordingLogger<HandlerStepConfigurationValidator>();

            await Validator(services, logger).StartAsync(CancellationToken.None);

            Assert.Single(logger.Entries);
        }

        [Fact]
        public async Task StepRegisteredBeforeItsHandler_DoesNotWarn()
        {
            var services = new ServiceCollection();
            AddStep<AuditStep>(services);
            AddHandler<AuditedOrderMessage>(services);
            var logger = new RecordingLogger<HandlerStepConfigurationValidator>();

            await Validator(services, logger).StartAsync(CancellationToken.None);

            Assert.Empty(logger.Entries);
        }

        [Fact]
        public async Task StepRegisteredAfterItsHandler_DoesNotWarn()
        {
            var services = new ServiceCollection();
            AddHandler<AuditedOrderMessage>(services);
            AddStep<AuditStep>(services);
            var logger = new RecordingLogger<HandlerStepConfigurationValidator>();

            await Validator(services, logger).StartAsync(CancellationToken.None);

            Assert.Empty(logger.Entries);
        }

        [Fact]
        public async Task StepMatchingOnlyAnEventHandler_DoesNotWarn()
        {
            var services = new ServiceCollection();
            services.AddSingleton(new Flowly.MessageInfrastructure.Events.EventHandlerSettings<AuditedOrderMessage, object>("topic", "subscription", "provider", "Handler"));
            AddStep<AuditStep>(services);
            var logger = new RecordingLogger<HandlerStepConfigurationValidator>();

            await Validator(services, logger).StartAsync(CancellationToken.None);

            Assert.Empty(logger.Entries);
        }

        [Fact]
        public async Task StepRegisteredTwice_WarnsOnceNamingStepAndCount()
        {
            var services = new ServiceCollection();
            AddHandler<AuditedOrderMessage>(services);
            AddStep<AuditStep>(services);
            AddStep<AuditStep>(services);
            var logger = new RecordingLogger<HandlerStepConfigurationValidator>();

            await Validator(services, logger).StartAsync(CancellationToken.None);

            var entry = Assert.Single(logger.Entries);
            Assert.Equal(LogLevel.Warning, entry.Level);
            Assert.Contains(typeof(AuditStep).ToString(), entry.Message);
            Assert.Contains("2 times", entry.Message);
        }

        [Fact]
        public async Task StepRegisteredTwiceMatchingNoHandler_WarnsOncePerProblem()
        {
            var services = new ServiceCollection();
            AddStep<AuditStep>(services);
            AddStep<AuditStep>(services);
            var logger = new RecordingLogger<HandlerStepConfigurationValidator>();

            await Validator(services, logger).StartAsync(CancellationToken.None);

            Assert.Equal(2, logger.Entries.Count);
        }

        [Fact]
        public async Task DifferentStepsWithSameFilter_DoNotWarnAboutDuplicates()
        {
            var services = new ServiceCollection();
            AddHandler<AuditedOrderMessage>(services);
            AddStep<AuditStep>(services);
            AddStep<OtherAuditStep>(services);
            var logger = new RecordingLogger<HandlerStepConfigurationValidator>();

            await Validator(services, logger).StartAsync(CancellationToken.None);

            Assert.Empty(logger.Entries);
        }

        [Fact]
        public async Task WithNoSteps_DoesNotWarn()
        {
            var services = new ServiceCollection();
            var logger = new RecordingLogger<HandlerStepConfigurationValidator>();

            await Validator(services, logger).StartAsync(CancellationToken.None);

            Assert.Empty(logger.Entries);
        }
    }

    private static HandlerStepConfigurationValidator Validator(ServiceCollection services, RecordingLogger<HandlerStepConfigurationValidator> logger)
        => new(
            services.Where(descriptor => descriptor.ServiceType == typeof(HandlerStepRegistration)).Select(descriptor => (HandlerStepRegistration)descriptor.ImplementationInstance!),
            new RegisteredHandlerMessageTypes(services),
            logger);

    private static void AddHandler<TMessage>(ServiceCollection services)
        => services.AddSingleton<IHandlerSettings<TMessage>>(new HandlerSettings<TMessage>("queue", "provider", "Handler", false, 1, 0, 0, 0, TimeSpan.Zero));

    private static void AddStep<TStep>(ServiceCollection services)
        where TStep : class, IHandlerStep
        => services.AddSingleton(new HandlerStepRegistration(typeof(TStep), HandlerStepRegistration.ResolveFilterType(typeof(TStep))));

    private interface IEnableAuditLog;

    private record PlainOrderMessage;

    private record AuditedOrderMessage : IEnableAuditLog;

    private sealed class AuditStep : HandlerStep<IEnableAuditLog>;

    private sealed class OtherAuditStep : HandlerStep<IEnableAuditLog>;

    private sealed class EveryMessageStep : HandlerStep;
}
