using Flowly.MessageInfrastructure;
using Flowly.MessageInfrastructure.StreamPartitions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Flowly.Tests;

public class MessageStreamHealthCheckRegistrationExtensionsTests
{
    public class AddMessageStreamHealthCheck
    {
        [Fact]
        public void RegistersTheCheckUnderTheDefaultNameAndTag()
        {
            var builder = new StubFlowlyBuilder();

            builder.AddMessageStreamHealthCheck();

            var registration = Registrations(builder).Single();
            Assert.Equal("flowly-message-streams", registration.Name);
            Assert.Equal(["message-streams"], registration.Tags);
            Assert.Equal(HealthStatus.Unhealthy, registration.FailureStatus);
        }

        [Fact]
        public void WithNameFailureStatusAndTags_UsesThem()
        {
            var builder = new StubFlowlyBuilder();

            builder.AddMessageStreamHealthCheck("streams", HealthStatus.Degraded, ["ready"]);

            var registration = Registrations(builder).Single();
            Assert.Equal("streams", registration.Name);
            Assert.Equal(["ready"], registration.Tags);
            Assert.Equal(HealthStatus.Degraded, registration.FailureStatus);
        }

        [Fact]
        public void ReturnsTheBuilder()
        {
            var builder = new StubFlowlyBuilder();

            Assert.Same(builder, builder.AddMessageStreamHealthCheck());
        }

        [Fact]
        public async Task ReportsTheStateOfTheRegisteredStateRegistry()
        {
            var builder = new StubFlowlyBuilder();
            builder.AddMessageStreamHealthCheck();
            using var provider = builder.Services.BuildServiceProvider();
            provider.GetRequiredService<IStreamPartitionStateRegistry>()
                .Set("OrderHandler", new StreamPartitionKey("orders", "OrderHandler", 1), StreamPartitionState.Halted, new InvalidOperationException("poison"));

            var registration = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations.Single();

            var result = await registration.Factory(provider).CheckHealthAsync(new HealthCheckContext { Registration = registration });

            Assert.Equal(HealthStatus.Unhealthy, result.Status);
        }

        [Fact]
        public void CalledTwiceWithDifferentNames_RegistersBothOnTheSameStateRegistry()
        {
            var builder = new StubFlowlyBuilder();

            builder.AddMessageStreamHealthCheck("first").AddMessageStreamHealthCheck("second");

            Assert.Equal(["first", "second"], Registrations(builder).Select(registration => registration.Name));
            Assert.Single(builder.Services, descriptor => descriptor.ServiceType == typeof(IStreamPartitionStateRegistry));
        }

        private static ICollection<HealthCheckRegistration> Registrations(StubFlowlyBuilder builder)
        {
            using var provider = builder.Services.BuildServiceProvider();

            return provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations;
        }
    }

    private sealed class StubFlowlyBuilder : IFlowlyBuilder
    {
        public IServiceCollection Services { get; } = new ServiceCollection();
        public IConfiguration Configuration { get; } = new ConfigurationBuilder().Build();
        public ITopologyNameResolver TopologyNameResolver => new KebabCaseTopologyNameResolver();
    }
}
