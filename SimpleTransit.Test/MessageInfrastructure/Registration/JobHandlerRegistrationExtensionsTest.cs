using AutoFixture;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SimpleTransit.AzureServiceBusWrappers;
using SimpleTransit.DatabaseModel.JobStateDatabase;
using SimpleTransit.MessageInfrastructure;
using SimpleTransit.MessageInfrastructure.BackgroundServices;
using SimpleTransit.MessageInfrastructure.Maintenance;
using SimpleTransit.MessageInfrastructure.MessageHandler;
using SimpleTransit.MessageInfrastructure.Messages;
using SimpleTransit.MessageInfrastructure.Model;
using SimpleTransit.MessageInfrastructure.Receivers;
using SimpleTransit.MessageInfrastructure.Registration;
using SimpleTransit.MessageInfrastructure.Senders;
using SimpleTransit.Repositories;
using SimpleTransit.Test.Utils;

namespace SimpleTransit.Test.MessageInfrastructure.Registration;

public class JobHandlerRegistrationExtensionsTest
{
    public class RegisterJobStateQueueProcessor
    {
        [Theory, AutoDataWithCustomization(typeof(SetupJobHandlerRegistrationExtensionsForTest))]
        public void MustRegisterAzureServiceBusWrapper(IServiceCollection serviceCollection)
        {
            serviceCollection.RegisterJobStateQueueProcessor();

            Assert.Contains(serviceCollection, descriptor =>
                descriptor.ServiceType == typeof(IServiceBusClient) &&
                descriptor.Lifetime == ServiceLifetime.Singleton);
        }

        [Theory, AutoDataWithCustomization(typeof(SetupJobHandlerRegistrationExtensionsForTest))]
        public void MustRegisterRecurringJobSchedulerBackgroundService(IServiceCollection serviceCollection)
        {
            serviceCollection.RegisterJobStateQueueProcessor();

            Assert.Contains(serviceCollection, descriptor =>
                descriptor.ServiceType == typeof(RecurringJobSchedulerBackgroundServiceOptions) &&
                descriptor.Lifetime == ServiceLifetime.Singleton);

            Assert.Contains(serviceCollection, descriptor =>
                descriptor.ServiceType == typeof(IHostedService) &&
                descriptor.ImplementationType == typeof(RecurringJobSchedulerBackgroundService) &&
                descriptor.Lifetime == ServiceLifetime.Singleton);
        }

        [Theory, AutoDataWithCustomization(typeof(SetupJobHandlerRegistrationExtensionsForTest))]
        public void MustRegisterRecurringJobInvoker(IServiceCollection serviceCollection)
        {
            serviceCollection.RegisterJobStateQueueProcessor();

            Assert.Contains(serviceCollection, descriptor =>
                descriptor.ServiceType == typeof(IRecurringJobInvoker) &&
                descriptor.ImplementationType == typeof(RecurringJobInvoker) &&
                descriptor.Lifetime == ServiceLifetime.Scoped);
        }

        [Theory, AutoDataWithCustomization(typeof(SetupJobHandlerRegistrationExtensionsForTest))]
        public void MustRegisterCreateJobStateMessageHandler(IServiceCollection serviceCollection)
        {
            serviceCollection.RegisterJobStateQueueProcessor();

            Assert.Contains(serviceCollection, descriptor =>
                descriptor.ServiceType == typeof(IMessageHandler<CreateJobState>) &&
                descriptor.ImplementationType == typeof(CreateJobStateHandler) &&
                descriptor.Lifetime == ServiceLifetime.Scoped);
        }

        [Theory, AutoDataWithCustomization(typeof(SetupJobHandlerRegistrationExtensionsForTest))]
        public void MustRegisterUpdateJobStateMessageHandler(IServiceCollection serviceCollection)
        {
            serviceCollection.RegisterJobStateQueueProcessor();

            Assert.Contains(serviceCollection, descriptor =>
                descriptor.ServiceType == typeof(IMessageHandler<UpdateJobState>) &&
                descriptor.ImplementationType == typeof(UpdateJobStateHandler) &&
                descriptor.Lifetime == ServiceLifetime.Scoped);
        }

        [Theory, AutoDataWithCustomization(typeof(SetupJobHandlerRegistrationExtensionsForTest))]
        public void MustRegisterUpdateCustomJobStateHandler(IServiceCollection serviceCollection)
        {
            serviceCollection.RegisterJobStateQueueProcessor();

            Assert.Contains(serviceCollection, descriptor =>
                descriptor.ServiceType == typeof(IMessageHandler<UpdateCustomJobState>) &&
                descriptor.ImplementationType == typeof(UpdateCustomJobStateHandler) &&
                descriptor.Lifetime == ServiceLifetime.Scoped);
        }

        [Theory, AutoDataWithCustomization(typeof(SetupJobHandlerRegistrationExtensionsForTest))]
        public void MustRegisterJobFailedHandler(IServiceCollection serviceCollection)
        {
            serviceCollection.RegisterJobStateQueueProcessor();

            Assert.Contains(serviceCollection, descriptor =>
                descriptor.ServiceType == typeof(IMessageHandler<JobFailed>) &&
                descriptor.ImplementationType == typeof(JobFailedHandler) &&
                descriptor.Lifetime == ServiceLifetime.Scoped);
        }

        [Theory, AutoDataWithCustomization(typeof(SetupJobHandlerRegistrationExtensionsForTest))]
        public void MustRegisterCreateRecurringJobStateHandler(IServiceCollection serviceCollection)
        {
            serviceCollection.RegisterJobStateQueueProcessor();

            Assert.Contains(serviceCollection, descriptor =>
                descriptor.ServiceType == typeof(IMessageHandler<CreateRecurringJobState>) &&
                descriptor.ImplementationType == typeof(CreateRecurringJobStateHandler) &&
                descriptor.Lifetime == ServiceLifetime.Scoped);
        }
    }

    public class AddRepositories
    {
        [Theory, AutoDataWithCustomization(typeof(SetupJobHandlerRegistrationExtensionsForTest))]
        public void MustAddDbContextFactoryAndRepository(IServiceCollection serviceCollection, string connectionString)
        {
            serviceCollection.AddRepositories(connectionString);

            Assert.Contains(serviceCollection, descriptor =>
                descriptor.ServiceType == typeof(IDbContextFactory<JobStateDataContext>) &&
                descriptor.Lifetime == ServiceLifetime.Singleton);

            Assert.Contains(serviceCollection, descriptor =>
                descriptor.ServiceType == typeof(IJobStateRepository) &&
                descriptor.ImplementationType == typeof(JobStateRepository) &&
                descriptor.Lifetime == ServiceLifetime.Scoped);
        }
    }

    public class AddJobHandler
    {
        [Theory, AutoDataWithCustomization(typeof(SetupJobHandlerRegistrationExtensionsForTest))]
        public void MustRegisterAzureServiceBusWrapper(IServiceCollection serviceCollection, string queueName)
        {
            serviceCollection.AddJobHandler<MyJobMessage, MyJobMessageHandler>(queueName);

            Assert.Contains(serviceCollection, descriptor =>
                descriptor.ServiceType == typeof(IServiceBusClient) &&
                descriptor.Lifetime == ServiceLifetime.Singleton);
        }

        [Theory, AutoDataWithCustomization(typeof(SetupJobHandlerRegistrationExtensionsForTest))]
        public void MustRegisterMessageHandlerAndBackgroundServiceProcessor(IServiceCollection serviceCollection, string queueName)
        {
            serviceCollection.AddJobHandler<MyJobMessage, MyJobMessageHandler>(queueName);

            Assert.Contains(serviceCollection, descriptor =>
                descriptor.ServiceType == typeof(IJobMessageHandler<MyJobMessage>) &&
                descriptor.ImplementationType == typeof(MyJobMessageHandler) &&
                descriptor.Lifetime == ServiceLifetime.Scoped);

            Assert.Contains(serviceCollection, descriptor =>
                descriptor.ServiceType == typeof(HandlerSettings<MyJobMessage>) &&
                descriptor.Lifetime == ServiceLifetime.Singleton);

            Assert.Contains(serviceCollection, descriptor =>
                descriptor.ServiceType == typeof(IHostedService) &&
                descriptor.ImplementationType == typeof(ServiceBusJobHandlerBackgroundService<MyJobMessage>) &&
                descriptor.Lifetime == ServiceLifetime.Singleton);
        }

        [Theory, AutoDataWithCustomization(typeof(SetupJobHandlerRegistrationExtensionsForTest))]
        public void MustRegisterAllJobStateSubmitters(IServiceCollection serviceCollection)
        {
            serviceCollection.AddJobHandler<MyJobMessage, MyJobMessageHandler>(QueuesNames.CreateJobState);

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
    }

    public class AddMessageHandler
    {
        [Theory, AutoDataWithCustomization(typeof(SetupJobHandlerRegistrationExtensionsForTest))]
        public void MustRegisterAzureServiceBusWrapper(IServiceCollection serviceCollection, string queueName)
        {
            serviceCollection.AddMessageHandler<MyMessage, MyMessageHandler>(queueName);

            Assert.Contains(serviceCollection, descriptor =>
                descriptor.ServiceType == typeof(IServiceBusClient) &&
                descriptor.Lifetime == ServiceLifetime.Singleton);
        }

        [Theory, AutoDataWithCustomization(typeof(SetupJobHandlerRegistrationExtensionsForTest))]
        public void MustRegisterMessageHandlerAndBackgroundServiceProcessor(IServiceCollection serviceCollection, string queueName)
        {
            serviceCollection.AddMessageHandler<MyMessage, MyMessageHandler>(queueName);

            Assert.Contains(serviceCollection, descriptor =>
                descriptor.ServiceType == typeof(IMessageHandler<MyMessage>) &&
                descriptor.ImplementationType == typeof(MyMessageHandler) &&
                descriptor.Lifetime == ServiceLifetime.Scoped);

            Assert.Contains(serviceCollection, descriptor =>
                descriptor.ServiceType == typeof(HandlerSettings<MyMessage>) &&
                descriptor.Lifetime == ServiceLifetime.Singleton);

            Assert.Contains(serviceCollection, descriptor =>
                descriptor.ServiceType == typeof(IHostedService) &&
                descriptor.ImplementationType == typeof(ServiceBusMessageHandlerBackgroundService<MyMessage>) &&
                descriptor.Lifetime == ServiceLifetime.Singleton);
        }
    }

    public class AddJobMaintenanceBackgroundJobs
    {
        [Theory, AutoDataWithCustomization(typeof(SetupJobHandlerRegistrationExtensionsForTest))]
        public void MustRegisterAzureServiceBusWrapper(IServiceCollection serviceCollection)
        {
            serviceCollection.AddJobMaintenanceBackgroundJobs();

            Assert.Contains(serviceCollection, descriptor =>
                descriptor.ServiceType == typeof(IServiceBusClient) &&
                descriptor.Lifetime == ServiceLifetime.Singleton);
        }

        [Theory, AutoDataWithCustomization(typeof(SetupJobHandlerRegistrationExtensionsForTest))]
        public void MustRegisterJobMaintenanceBackgroundJobs(IServiceCollection serviceCollection)
        {
            serviceCollection.AddJobMaintenanceBackgroundJobs();

            Assert.Contains(serviceCollection, descriptor =>
                descriptor.ServiceType == typeof(IHostedService) &&
                descriptor.ImplementationType == typeof(RecurringJobHandlerBackgroundService<RemoveOldJobsRecurringJob>) &&
                descriptor.Lifetime == ServiceLifetime.Singleton);

            Assert.Contains(serviceCollection, descriptor =>
                descriptor.ServiceType == typeof(IHostedService) &&
                descriptor.ImplementationType == typeof(RecurringJobHandlerBackgroundService<FailHungJobsRecurringJob>) &&
                descriptor.Lifetime == ServiceLifetime.Singleton);
        }

        [Theory, AutoDataWithCustomization(typeof(SetupJobHandlerRegistrationExtensionsForTest))]
        public void MustRegisterCreateRecurringJobStateSubmitter(IServiceCollection serviceCollection)
        {
            serviceCollection.AddJobMaintenanceBackgroundJobs();

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

    private class SetupJobHandlerRegistrationExtensionsForTest : ICustomization
    {
        public void Customize(IFixture fixture)
        {
            var serviceCollection = new ServiceCollection();
            fixture.Inject<IServiceCollection>(serviceCollection);
        }
    }

    private record MyJobMessage(string Description, string JobTypeName) : IJobMessage;

    private class MyJobMessageHandler : IJobMessageHandler<MyJobMessage>
    {
        public Task Handle(IJobMessageContext<MyJobMessage> messageContext) => Task.CompletedTask;
    }

    private record MyMessage;

    private class MyMessageHandler : IMessageHandler<MyMessage>
    {
        public Task Handle(IMessageContext<MyMessage> messageContext) => Task.CompletedTask;
    }
}