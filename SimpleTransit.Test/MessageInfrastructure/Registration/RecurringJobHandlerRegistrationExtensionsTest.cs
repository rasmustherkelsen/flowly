using AutoFixture;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SimpleTransit.MessageInfrastructure.BackgroundServices;
using SimpleTransit.MessageInfrastructure.Messages;
using SimpleTransit.MessageInfrastructure.RecurringJobs;
using SimpleTransit.MessageInfrastructure.Registration;
using SimpleTransit.MessageInfrastructure.Senders;
using SimpleTransit.Test.Utils;

namespace SimpleTransit.Test.MessageInfrastructure.Registration;

public class RecurringJobHandlerRegistrationExtensionsTest
{
    public class AddRecurringJob
    {
        [Theory, AutoDataWithCustomization(typeof(SetupRecurringJobHandlerRegistrationExtensionsForTest))]
        public void MustAddSettings(IServiceCollection serviceCollection)
        {
            serviceCollection.AddRecurringJob<TestRecurringJobHandler>("Test job", TimeSpan.FromSeconds(5));

            var serviceProvider = serviceCollection.BuildServiceProvider();
            var jobHandlerSettings = serviceProvider.GetRequiredService<RecurringJobHandlerBackgroundService<TestRecurringJobHandler>.RecurringJobSettings>();

            Assert.Equal("Test job", jobHandlerSettings.JobDescription);
            Assert.Equal(nameof(TestRecurringJobHandler), jobHandlerSettings.SessionName);
            Assert.Equal(TimeSpan.FromSeconds(5), jobHandlerSettings.Interval);
        }

        [Theory, AutoDataWithCustomization(typeof(SetupRecurringJobHandlerRegistrationExtensionsForTest))]
        public void MustAddBackgroundProcessorHandler(IServiceCollection serviceCollection)
        {
            serviceCollection.AddRecurringJob<TestRecurringJobHandler>("Test job", TimeSpan.FromSeconds(5));

            Assert.Contains(serviceCollection, descriptor =>
                descriptor.ServiceType == typeof(IHostedService) &&
                descriptor.ImplementationType == typeof(RecurringJobHandlerBackgroundService<TestRecurringJobHandler>) &&
                descriptor.Lifetime == ServiceLifetime.Singleton);

            Assert.Contains(serviceCollection, descriptor =>
                descriptor.ServiceType == typeof(TestRecurringJobHandler) &&
                descriptor.ImplementationType == typeof(TestRecurringJobHandler) &&
                descriptor.Lifetime == ServiceLifetime.Scoped);
        }

        [Theory, AutoDataWithCustomization(typeof(SetupRecurringJobHandlerRegistrationExtensionsForTest))]
        public void MustAddCreateRecurringJobStateMessageSubmitter(IServiceCollection serviceCollection)
        {
            serviceCollection.AddRecurringJob<TestRecurringJobHandler>("Test job", TimeSpan.FromSeconds(5));

            Assert.Contains(serviceCollection, descriptor =>
                descriptor.ServiceType == typeof(IMessageSubmitter<CreateRecurringJobState>) &&
                descriptor.ImplementationType == typeof(MessageSubmitter<CreateRecurringJobState>) &&
                descriptor.Lifetime == ServiceLifetime.Singleton);

            Assert.Contains(serviceCollection, descriptor =>
                descriptor.ServiceType == typeof(IMessageSender) &&
                descriptor.ImplementationType == typeof(MessageSender) &&
                descriptor.Lifetime == ServiceLifetime.Singleton);
        }
    }

    private class SetupRecurringJobHandlerRegistrationExtensionsForTest : ICustomization
    {
        public void Customize(IFixture fixture)
        {
            var serviceCollection = new ServiceCollection();
            fixture.Inject<IServiceCollection>(serviceCollection);
        }
    }
}

public class TestRecurringJobHandler : IRecurringJobHandler
{
    public Task Handle(CancellationToken cancellationToken) => Task.CompletedTask;
}