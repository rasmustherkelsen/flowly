using Flowly.Jobs.BackgroundServices;
using Flowly.Jobs.Tests.Fakes;
using Flowly.MessageInfrastructure.Registration;
using Flowly.Registration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.Jobs.Tests.Registration;

public class RecurringJobHandlerRegistrationExtensionsTests
{
    private static IFlowlyBuilder BuildBuilder()
    {
        return BuildBuilderWithManifest().Builder;
    }

    private static (IFlowlyBuilder Builder, ProviderQueueManifest Manifest) BuildBuilderWithManifest(string providerName = "primary")
    {
        var services = new ServiceCollection();
        var registry = new FakeMessageBusClientRegistry(new FakeMessageBusClient(), providerName);
        services.AddSingleton<IMessageBusClientRegistry>(registry);
        var manifest = new ProviderQueueManifest(providerName, true, "Fake");
        services.AddSingleton(manifest);
        services.AddSingleton<IQueueRegistrar, QueueRegistrar>();
        var builder = new StubFlowlyBuilder(services);
        return (builder, manifest);
    }

    public class AddRecurringJob
    {
        [Fact]
        public void RegistersHandlerAsScoped()
        {
            var builder = BuildBuilder();

            builder.AddRecurringJob<SomeRecurringJob>();

            var descriptor = builder.Services.FirstOrDefault(s => s.ServiceType == typeof(SomeRecurringJob));
            Assert.NotNull(descriptor);
            Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
        }

        [Fact]
        public void RegistersSettingsSingletonWithDescriptionCronAndHandlerTypeName()
        {
            var builder = BuildBuilder();

            builder.AddRecurringJob<SomeRecurringJob>();

            var settings = builder.Services
                .Where(s => s.ServiceType == typeof(RecurringJobHandlerBackgroundService<SomeRecurringJob>.RecurringJobSettings))
                .Select(s => (RecurringJobHandlerBackgroundService<SomeRecurringJob>.RecurringJobSettings)s.ImplementationInstance!)
                .Single();

            Assert.Equal("Runs every minute", settings.JobDescription);
            Assert.Equal("* * * * *", settings.CronExpression);
            Assert.Equal(nameof(SomeRecurringJob), settings.SessionName);
        }

        [Fact]
        public void RegistersRecurringJobBackgroundService()
        {
            var builder = BuildBuilder();

            builder.AddRecurringJob<SomeRecurringJob>();

            var descriptor = builder.Services.FirstOrDefault(s =>
                s.ImplementationType?.IsGenericType == true
                && s.ImplementationType.GetGenericTypeDefinition() == typeof(RecurringJobHandlerBackgroundService<>));
            Assert.NotNull(descriptor);
        }

        [Fact]
        public void RegistersRecurringJobsQueueOnPrimaryProviderManifest()
        {
            var (builder, manifest) = BuildBuilderWithManifest();

            builder.AddRecurringJob<SomeRecurringJob>();

            Assert.Contains(manifest.Queues, q => q.QueueName == JobQueuesNames.RecurringJobs && q.RequiresSession);
        }
    }

    private sealed class StubFlowlyBuilder(IServiceCollection services) : IFlowlyBuilder
    {
        public IServiceCollection Services => services;
        public IConfiguration Configuration => new ConfigurationBuilder().Build();
    }

    [RecurringJob("Runs every minute", "* * * * *")]
    private class SomeRecurringJob : RecurringJobHandler
    {
        public override Task Handle(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}