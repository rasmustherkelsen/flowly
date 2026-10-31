using Flowly.MessageInfrastructure.StreamPartitions;
using Flowly.Tests.Fakes;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Flowly.Tests.MessageInfrastructure.StreamPartitions;

public class StreamPartitionHealthCheckTests
{
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static Task<HealthCheckResult> Check(StreamPartitionStateRegistry registry, HealthStatus failureStatus = HealthStatus.Unhealthy)
    {
        var streamPartitionHealthCheck = new StreamPartitionHealthCheck(registry);
        var context = new HealthCheckContext { Registration = new HealthCheckRegistration("streams", streamPartitionHealthCheck, failureStatus, null) };

        return streamPartitionHealthCheck.CheckHealthAsync(context);
    }

    private static StreamPartitionStateRegistry CreateRegistry() => new(new ManualTimeProvider(Start));

    public class CheckHealthAsync
    {
        [Fact]
        public async Task WhenNoPartitionIsOwned_IsHealthy()
        {
            var result = await Check(CreateRegistry());

            Assert.Equal(HealthStatus.Healthy, result.Status);
        }

        [Fact]
        public async Task WhenEveryPartitionIsRunning_IsHealthy()
        {
            var registry = CreateRegistry();
            registry.Set("OrderHandler", new StreamPartitionKey("orders", "OrderHandler", 0), StreamPartitionState.Running, null);
            registry.Set("OrderHandler", new StreamPartitionKey("orders", "OrderHandler", 1), StreamPartitionState.Running, null);

            var result = await Check(registry);

            Assert.Equal(HealthStatus.Healthy, result.Status);
            Assert.Empty(result.Data);
        }

        [Fact]
        public async Task WhenAPartitionIsHalted_IsUnhealthyAndNamesItWithItsLastError()
        {
            var registry = CreateRegistry();
            registry.Set("OrderHandler", new StreamPartitionKey("orders", "OrderHandler", 0), StreamPartitionState.Running, null);
            registry.Set("OrderHandler", new StreamPartitionKey("orders", "OrderHandler", 1), StreamPartitionState.Halted, new InvalidOperationException("poison"));

            var result = await Check(registry);

            Assert.Equal(HealthStatus.Unhealthy, result.Status);
            var detail = Assert.Single(result.Data);
            Assert.Equal("orders/OrderHandler/1", detail.Key);
            Assert.Contains("Halted since", (string)detail.Value);
            Assert.Contains("System.InvalidOperationException: poison", (string)detail.Value);
        }

        [Fact]
        public async Task WhenAPartitionIsWaiting_IsUnhealthyAndNamesItWithItsLastError()
        {
            var registry = CreateRegistry();
            registry.Set("OrderHandler", new StreamPartitionKey("orders", "OrderHandler", 2), StreamPartitionState.Waiting, new TimeoutException("store down"));

            var result = await Check(registry);

            Assert.Equal(HealthStatus.Unhealthy, result.Status);
            Assert.Contains("Waiting since", (string)result.Data["orders/OrderHandler/2"]);
            Assert.Contains("store down", (string)result.Data["orders/OrderHandler/2"]);
        }

        [Fact]
        public async Task ForANonPartitionedStream_NamesItWithoutAPartition()
        {
            var registry = CreateRegistry();
            registry.Set("OrderHandler", new StreamPartitionKey("orders", "OrderHandler", null), StreamPartitionState.Halted, new InvalidOperationException("poison"));

            var result = await Check(registry);

            Assert.Equal(HealthStatus.Unhealthy, result.Status);
            Assert.True(result.Data.ContainsKey("orders/OrderHandler"));
        }

        [Fact]
        public async Task WhenOnlyAHaltedPartitionHasRecovered_IsHealthyAgain()
        {
            var registry = CreateRegistry();
            var key = new StreamPartitionKey("orders", "OrderHandler", 1);
            registry.Set("OrderHandler", key, StreamPartitionState.Halted, new InvalidOperationException("poison"));

            registry.Set("OrderHandler", key, StreamPartitionState.Running, null);

            Assert.Equal(HealthStatus.Healthy, (await Check(registry)).Status);
        }

        [Fact]
        public async Task WhenTheFailingPartitionIsRevoked_IsHealthyAgain()
        {
            var registry = CreateRegistry();
            var key = new StreamPartitionKey("orders", "OrderHandler", 1);
            registry.Set("OrderHandler", key, StreamPartitionState.Halted, new InvalidOperationException("poison"));

            registry.Remove(key);

            Assert.Equal(HealthStatus.Healthy, (await Check(registry)).Status);
        }

        [Fact]
        public async Task WhenTheRegistrationFailureStatusIsDegraded_ReportsDegraded()
        {
            var registry = CreateRegistry();
            registry.Set("OrderHandler", new StreamPartitionKey("orders", "OrderHandler", 1), StreamPartitionState.Halted, new InvalidOperationException("poison"));

            var result = await Check(registry, HealthStatus.Degraded);

            Assert.Equal(HealthStatus.Degraded, result.Status);
        }
    }
}
