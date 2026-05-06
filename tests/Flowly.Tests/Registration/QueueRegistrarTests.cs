using Flowly.MessageInfrastructure.Registration;
using Flowly.Registration;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.Tests.Registration;

public class QueueRegistrarTests
{
    public class RegisterWithRegistration
    {
        [Fact]
        public void AddsQueueToManifest()
        {
            var services = CreateServices();

            var queueRegistrar = new QueueRegistrar();
            var registration = new DeferredQueueRegistration("orders");

            queueRegistrar.Register(services, registration);

            var manifest = GetManifest(services);
            Assert.Single(manifest.Queues);
            Assert.Equal("orders", manifest.Queues[0].QueueName);
        }

        [Fact]
        public void WithEmptyQueueName_ReturnsEarly()
        {
            var services = CreateServices();

            var queueRegistrar = new QueueRegistrar();
            var registration = new DeferredQueueRegistration("");

            queueRegistrar.Register(services, registration);

            var manifest = GetManifest(services);
            Assert.Empty(manifest.Queues);
        }

        [Fact]
        public void WithWhiteSpaceQueueName_ReturnsEarly()
        {
            var services = CreateServices();

            var queueRegistrar = new QueueRegistrar();
            var registration = new DeferredQueueRegistration("   ");

            queueRegistrar.Register(services, registration);

            var manifest = GetManifest(services);
            Assert.Empty(manifest.Queues);
        }

        [Fact]
        public void WithProviderName_UsesSpecifiedProvider()
        {
            var services = CreateServices();

            var queueRegistrar = new QueueRegistrar();
            var registration = new DeferredQueueRegistration("orders");

            queueRegistrar.Register(services, registration, "azure-service-bus");

            var manifest = GetManifest(services, "azure-service-bus");
            Assert.Single(manifest.Queues);
            Assert.Equal("orders", manifest.Queues[0].QueueName);
        }

        [Fact]
        public void DeduplicatesQueuesWithSameName()
        {
            var services = CreateServices();

            var queueRegistrar = new QueueRegistrar();
            var registration1 = new DeferredQueueRegistration("orders", false, null, null, TimeSpan.FromMinutes(5));
            var registration2 = new DeferredQueueRegistration("orders", true, TimeSpan.FromHours(2), true, null);

            queueRegistrar.Register(services, registration1);
            queueRegistrar.Register(services, registration2);

            var manifest = GetManifest(services);
            Assert.Single(manifest.Queues);
            Assert.True(manifest.Queues[0].RequiresSession);
            Assert.Equal(TimeSpan.FromHours(2), manifest.Queues[0].DefaultMessageTimeToLive);
            Assert.True(manifest.Queues[0].DeadLetterOnMessageExpiration);
            Assert.Equal(TimeSpan.FromMinutes(5), manifest.Queues[0].LockDuration);
        }

        [Fact]
        public void WithConflictingSettings_Throws()
        {
            var services = CreateServices();

            var queueRegistrar = new QueueRegistrar();

            queueRegistrar.Register(services, new DeferredQueueRegistration("orders", false, null, null, TimeSpan.FromHours(1)));

            Assert.Throws<InvalidOperationException>(
                () => queueRegistrar.Register(services, new DeferredQueueRegistration("orders", false, null, null, TimeSpan.FromHours(2))));
        }

        [Fact]
        public void WithUnregisteredProviderName_Throws()
        {
            var services = CreateServices();

            var queueRegistrar = new QueueRegistrar();
            var registration = new DeferredQueueRegistration("orders");

            Assert.Throws<InvalidOperationException>(
                () => queueRegistrar.Register(services, registration, "nonexistent-provider"));
        }
    }

    public class RegisterWithName
    {
        [Fact]
        public void CreatesDeferredRegistrationWithCorrectName()
        {
            var services = CreateServices();

            var queueRegistrar = new QueueRegistrar();

            queueRegistrar.Register(services, "jobs", requiresSession: true);

            var manifest = GetManifest(services);
            Assert.Single(manifest.Queues);
            Assert.Equal("jobs", manifest.Queues[0].QueueName);
            Assert.True(manifest.Queues[0].RequiresSession);
        }

        [Fact]
        public void DefaultsRequiresSessionToFalse()
        {
            var services = CreateServices();

            var queueRegistrar = new QueueRegistrar();

            queueRegistrar.Register(services, "jobs");

            var manifest = GetManifest(services);
            Assert.False(manifest.Queues[0].RequiresSession);
        }
    }

    private static ServiceCollection CreateServices()
    {
        var services = new ServiceCollection();
        var registry = new MessageBusClientRegistry();
        registry.Register("azure-service-bus", null!, null);
        services.AddSingleton<IMessageBusClientRegistry>(registry);
        services.AddSingleton(new ProviderQueueManifest("azure-service-bus", true, "AzureServiceBus"));
        return services;
    }

    private static ProviderQueueManifest GetManifest(IServiceCollection services) => GetManifest(services, "azure-service-bus");

    private static ProviderQueueManifest GetManifest(IServiceCollection services, string providerName) =>
        services
            .Where(s => s.ImplementationInstance is ProviderQueueManifest m && m.ProviderName == providerName)
            .Select(s => (ProviderQueueManifest)s.ImplementationInstance!)
            .First();
}
