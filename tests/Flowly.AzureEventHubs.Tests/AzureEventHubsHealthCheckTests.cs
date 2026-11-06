using Flowly.AzureEventHubs.Tests.Fakes;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Flowly.AzureEventHubs.Tests;

public class AzureEventHubsHealthCheckTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    public class CheckHealthAsync
    {
        [Fact]
        public async Task WhenEveryEventHubIsReachable_ReturnsHealthyNamingTheProbedEventHubs()
        {
            var clientFactory = ClientFactory(("orders", new FakeEventHubsInspector(["0"])), ("readings", new FakeEventHubsInspector(["0", "1"])));
            var azureEventHubsHealthCheck = Create(clientFactory, "orders", "readings");

            var result = await azureEventHubsHealthCheck.CheckHealthAsync(new HealthCheckContext());

            Assert.Equal(HealthStatus.Healthy, result.Status);
            Assert.Contains("'orders'", result.Description);
            Assert.Contains("'readings'", result.Description);
        }

        [Fact]
        public async Task WhenEveryEventHubIsReachable_ProbesEachEventHubOnce()
        {
            var orders = new FakeEventHubsInspector(["0"]);
            var readings = new FakeEventHubsInspector(["0"]);
            var azureEventHubsHealthCheck = Create(ClientFactory(("orders", orders), ("readings", readings)), "orders", "readings");

            await azureEventHubsHealthCheck.CheckHealthAsync(new HealthCheckContext());

            Assert.Equal(1, orders.Calls);
            Assert.Equal(1, readings.Calls);
        }

        [Fact]
        public async Task WhenOneEventHubFails_ReturnsUnhealthyNamingOnlyTheFailingEventHub()
        {
            var failure = new InvalidOperationException("denied");
            var clientFactory = ClientFactory(("orders", new FakeEventHubsInspector(["0"])), ("readings", new FakeEventHubsInspector(failure: failure)));
            var azureEventHubsHealthCheck = Create(clientFactory, "orders", "readings");

            var result = await azureEventHubsHealthCheck.CheckHealthAsync(new HealthCheckContext());

            Assert.Equal(HealthStatus.Unhealthy, result.Status);
            Assert.Contains("'readings'", result.Description);
            Assert.DoesNotContain("'orders'", result.Description);
            Assert.Same(failure, result.Exception);
        }

        [Fact]
        public async Task WhenSeveralEventHubsFail_ReturnsUnhealthyNamingEachWithAnAggregateException()
        {
            var clientFactory = ClientFactory(
                ("orders", new FakeEventHubsInspector(failure: new InvalidOperationException("first"))),
                ("readings", new FakeEventHubsInspector(failure: new InvalidOperationException("second"))));
            var azureEventHubsHealthCheck = Create(clientFactory, "orders", "readings");

            var result = await azureEventHubsHealthCheck.CheckHealthAsync(new HealthCheckContext());

            Assert.Equal(HealthStatus.Unhealthy, result.Status);
            Assert.Contains("'orders'", result.Description);
            Assert.Contains("'readings'", result.Description);
            Assert.Equal(2, Assert.IsType<AggregateException>(result.Exception).InnerExceptions.Count);
        }

        [Fact]
        public async Task WhenTheInspectorCannotBeCreated_ReturnsUnhealthy()
        {
            var azureEventHubsHealthCheck = Create(new FakeEventHubsClientFactory(), "orders");

            var result = await azureEventHubsHealthCheck.CheckHealthAsync(new HealthCheckContext());

            Assert.Equal(HealthStatus.Unhealthy, result.Status);
            Assert.Contains("'orders'", result.Description);
        }

        [Fact]
        public async Task WhenNoEventHubIsRegistered_ReturnsHealthyWithoutProbing()
        {
            var clientFactory = new FakeEventHubsClientFactory();
            var azureEventHubsHealthCheck = Create(clientFactory);

            var result = await azureEventHubsHealthCheck.CheckHealthAsync(new HealthCheckContext());

            Assert.Equal(HealthStatus.Healthy, result.Status);
            Assert.Contains("nothing to probe", result.Description);
            Assert.Empty(clientFactory.Inspectors);
        }

        [Fact]
        public async Task WhenAProbeDoesNotFinishWithinTheTimeout_ReturnsUnhealthyNamingTheEventHub()
        {
            var timeProvider = new ScheduledTimeProvider();
            var clientFactory = ClientFactory(("orders", new FakeEventHubsInspector(["0"])), ("readings", new FakeEventHubsInspector(neverCompletes: true)));
            var azureEventHubsHealthCheck = Create(clientFactory, timeProvider, "orders", "readings");

            var check = azureEventHubsHealthCheck.CheckHealthAsync(new HealthCheckContext());
            timeProvider.Advance(Timeout);
            var result = await check;

            Assert.Equal(HealthStatus.Unhealthy, result.Status);
            Assert.Contains("'readings' did not respond within 5 seconds", result.Description);
            Assert.DoesNotContain("'orders'", result.Description);
            Assert.IsAssignableFrom<OperationCanceledException>(result.Exception);
        }

        [Fact]
        public async Task WhenAProbeFinishesBeforeTheTimeout_ReturnsHealthyAndReleasesTheTimer()
        {
            var timeProvider = new ScheduledTimeProvider();
            var azureEventHubsHealthCheck = Create(ClientFactory(("orders", new FakeEventHubsInspector(["0"]))), timeProvider, "orders");

            var result = await azureEventHubsHealthCheck.CheckHealthAsync(new HealthCheckContext());

            Assert.Equal(HealthStatus.Healthy, result.Status);
            Assert.Equal(0, timeProvider.PendingTimers);
        }

        [Fact]
        public async Task WhenTheCallerCancels_ReturnsUnhealthyWithoutReportingATimeout()
        {
            using var cancellation = new CancellationTokenSource();
            var clientFactory = ClientFactory(("orders", new FakeEventHubsInspector(neverCompletes: true)));
            var azureEventHubsHealthCheck = Create(clientFactory, new ScheduledTimeProvider(), "orders");

            var check = azureEventHubsHealthCheck.CheckHealthAsync(new HealthCheckContext(), cancellation.Token);
            await cancellation.CancelAsync();
            var result = await check;

            Assert.Equal(HealthStatus.Unhealthy, result.Status);
            Assert.DoesNotContain("did not respond", result.Description);
        }

        [Fact]
        public async Task CalledAgain_ProbesAgainInsteadOfReusingTheResult()
        {
            var orders = new FakeEventHubsInspector(["0"]);
            var azureEventHubsHealthCheck = Create(ClientFactory(("orders", orders)), "orders");

            await azureEventHubsHealthCheck.CheckHealthAsync(new HealthCheckContext());
            await azureEventHubsHealthCheck.CheckHealthAsync(new HealthCheckContext());

            Assert.Equal(2, orders.Calls);
        }

        [Fact]
        public async Task EventHubsRegisteredAfterTheCheckWasCreated_AreProbed()
        {
            var names = new List<string>();
            var orders = new FakeEventHubsInspector(["0"]);
            var azureEventHubsHealthCheck = new AzureEventHubsHealthCheck(ClientFactory(("orders", orders)), () => names, Timeout, TimeProvider.System);

            names.Add("orders");
            await azureEventHubsHealthCheck.CheckHealthAsync(new HealthCheckContext());

            Assert.Equal(1, orders.Calls);
        }
    }

    public class DefaultTimeout
    {
        [Fact]
        public void IsFiveSeconds() => Assert.Equal(TimeSpan.FromSeconds(5), AzureEventHubsHealthCheck.DefaultTimeout);
    }

    private static AzureEventHubsHealthCheck Create(FakeEventHubsClientFactory clientFactory, params string[] eventHubNames)
        => Create(clientFactory, TimeProvider.System, eventHubNames);

    private static AzureEventHubsHealthCheck Create(FakeEventHubsClientFactory clientFactory, TimeProvider timeProvider, params string[] eventHubNames)
        => new(clientFactory, () => eventHubNames, Timeout, timeProvider);

    private static FakeEventHubsClientFactory ClientFactory(params (string EventHubName, FakeEventHubsInspector Inspector)[] inspectors)
    {
        var clientFactory = new FakeEventHubsClientFactory();

        foreach (var (eventHubName, inspector) in inspectors)
        {
            clientFactory.Inspectors[eventHubName] = inspector;
        }

        return clientFactory;
    }
}
