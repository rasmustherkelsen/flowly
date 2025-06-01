using AutoFixture;
using Microsoft.Extensions.DependencyInjection;
using SimpleTransit.MessageInfrastructure;
using SimpleTransit.MessageInfrastructure.Messages;
using SimpleTransit.MessageInfrastructure.Registration;
using SimpleTransit.MessageInfrastructure.Senders;
using SimpleTransit.Test.Utils;

namespace SimpleTransit.Test.MessageInfrastructure.Registration;

public class SubmitterRegistrationExtensionsTest
{
    public class AddJobStateSubmitters
    {
        [Theory, AutoDataWithCustomization(typeof(SetupSubmitterRegistrationExtensionsForTest))]
        public void MustRegisterJobStateSubmitters(IServiceCollection serviceCollection)
        {
            serviceCollection.AddJobStateSubmitters();

            Assert.Contains(serviceCollection, descriptor =>
                descriptor.ServiceType == typeof(IMessageSubmitter<CreateJobState>) &&
                descriptor.ImplementationType == typeof(MessageSubmitter<CreateJobState>) &&
                descriptor.Lifetime == ServiceLifetime.Singleton);

            Assert.Contains(serviceCollection, descriptor =>
                descriptor.ServiceType == typeof(IMessageSubmitter<UpdateJobState>) &&
                descriptor.ImplementationType == typeof(MessageSubmitter<UpdateJobState>) &&
                descriptor.Lifetime == ServiceLifetime.Singleton);

            Assert.Contains(serviceCollection, descriptor =>
                descriptor.ServiceType == typeof(IMessageSubmitter<JobFailed>) &&
                descriptor.ImplementationType == typeof(MessageSubmitter<JobFailed>) &&
                descriptor.Lifetime == ServiceLifetime.Singleton);

            Assert.Contains(serviceCollection, descriptor =>
                descriptor.ServiceType == typeof(IMessageSubmitter<UpdateCustomJobState>) &&
                descriptor.ImplementationType == typeof(MessageSubmitter<UpdateCustomJobState>) &&
                descriptor.Lifetime == ServiceLifetime.Singleton);
        }

        [Theory, AutoDataWithCustomization(typeof(SetupSubmitterRegistrationExtensionsForTest))]
        public void MustRegisterMessageSender(IServiceCollection serviceCollection)
        {
            serviceCollection.AddJobStateSubmitters();

            Assert.Contains(serviceCollection, descriptor =>
                descriptor.ServiceType == typeof(IMessageSender) &&
                descriptor.ImplementationType == typeof(MessageSender) &&
                descriptor.Lifetime == ServiceLifetime.Singleton);
        }

        [Theory, AutoDataWithCustomization(typeof(SetupSubmitterRegistrationExtensionsForTest))]
        public void MustRegisterCorrectQueueNames(IServiceCollection serviceCollection)
        {
            serviceCollection.AddJobStateSubmitters();

            var serviceProvider = serviceCollection.BuildServiceProvider();

            var createJobStateSettings = serviceProvider.GetRequiredService<MessageSubmitter<CreateJobState>.QueueSettings>();
            Assert.Equal(QueuesNames.CreateJobState, createJobStateSettings.QueueName);

            var updateJobStateSettings = serviceProvider.GetRequiredService<MessageSubmitter<UpdateJobState>.QueueSettings>();
            Assert.Equal(QueuesNames.UpdateJobState, updateJobStateSettings.QueueName);

            var jobFailedSettings = serviceProvider.GetRequiredService<MessageSubmitter<JobFailed>.QueueSettings>();
            Assert.Equal(QueuesNames.JobFailed, jobFailedSettings.QueueName);

            var updateCustomJobStateSettings = serviceProvider.GetRequiredService<MessageSubmitter<UpdateCustomJobState>.QueueSettings>();
            Assert.Equal(QueuesNames.UpdateJobCustomState, updateCustomJobStateSettings.QueueName);
        }
    }

    private class SetupSubmitterRegistrationExtensionsForTest : ICustomization
    {
        public void Customize(IFixture fixture)
        {
            fixture.Inject<IServiceCollection>(new ServiceCollection());
        }
    }
}
