using System.Text.RegularExpressions;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Flowly.AzureEventHubs.Tests.Emulator;

public partial class HealthCheckTests
{
    private const string MissingHub = "it-missing";

    [Collection(EmulatorCollection.Name)]
    public class CheckHealthAsync
    {
        [EmulatorFact]
        public async Task EventHubsThatExist_AreHealthy()
        {
            await using var clientFactory = EventHubsClientFactory.ForConnectionString(EmulatorEnvironment.RequiredConnectionString);
            var azureEventHubsHealthCheck = Create(clientFactory, EmulatorEntities.Single, EmulatorEntities.Partitioned);

            var result = await azureEventHubsHealthCheck.CheckHealthAsync(new HealthCheckContext());

            Assert.Equal(HealthStatus.Healthy, result.Status);
            Assert.Contains(EmulatorEntities.Single, result.Description);
            Assert.Contains(EmulatorEntities.Partitioned, result.Description);
        }

        [EmulatorFact]
        public async Task AnEventHubThatDoesNotExist_IsUnhealthyNamingIt()
        {
            await using var clientFactory = EventHubsClientFactory.ForConnectionString(EmulatorEnvironment.RequiredConnectionString);
            var azureEventHubsHealthCheck = Create(clientFactory, EmulatorEntities.Single, MissingHub);

            var result = await azureEventHubsHealthCheck.CheckHealthAsync(new HealthCheckContext());

            Assert.Equal(HealthStatus.Unhealthy, result.Status);
            Assert.Contains($"'{MissingHub}'", result.Description);
            Assert.DoesNotContain($"'{EmulatorEntities.Single}'", result.Description);
        }

        [EmulatorFact]
        public async Task AnAddressNothingListensOn_IsUnhealthy()
        {
            var unreachableConnectionString = EndpointRegex().Replace(EmulatorEnvironment.RequiredConnectionString, "Endpoint=sb://127.0.0.1:1;");
            await using var clientFactory = EventHubsClientFactory.ForConnectionString(unreachableConnectionString);
            var azureEventHubsHealthCheck = Create(clientFactory, EmulatorEntities.Single);

            var result = await azureEventHubsHealthCheck.CheckHealthAsync(new HealthCheckContext());

            Assert.Equal(HealthStatus.Unhealthy, result.Status);
            Assert.Contains($"'{EmulatorEntities.Single}'", result.Description);
        }

        private static AzureEventHubsHealthCheck Create(IEventHubsClientFactory clientFactory, params string[] eventHubNames)
            => new(clientFactory, () => eventHubNames, AzureEventHubsHealthCheck.DefaultTimeout, TimeProvider.System);
    }

    [GeneratedRegex("Endpoint=sb://[^;]+;")]
    private static partial Regex EndpointRegex();
}
