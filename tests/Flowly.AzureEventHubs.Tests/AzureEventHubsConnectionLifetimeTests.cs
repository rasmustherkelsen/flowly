using Flowly.AzureEventHubs.Tests.Fakes;
using Flowly.MessageInfrastructure.Registration;

namespace Flowly.AzureEventHubs.Tests;

public class AzureEventHubsConnectionLifetimeTests
{
    public class StartAsync
    {
        [Fact]
        public async Task DoesNotDisposeTheClient()
        {
            var clientFactory = new FakeEventHubsClientFactory();
            var azureEventHubsConnectionLifetime = new AzureEventHubsConnectionLifetime(new AzureEventHubsClient(clientFactory, null, new StreamQueueManifest()));

            await azureEventHubsConnectionLifetime.StartAsync(CancellationToken.None);

            Assert.False(clientFactory.Disposed);
        }
    }

    public class StopAsync
    {
        [Fact]
        public async Task DisposesTheClient()
        {
            var clientFactory = new FakeEventHubsClientFactory();
            var azureEventHubsConnectionLifetime = new AzureEventHubsConnectionLifetime(new AzureEventHubsClient(clientFactory, null, new StreamQueueManifest()));

            await azureEventHubsConnectionLifetime.StopAsync(CancellationToken.None);

            Assert.True(clientFactory.Disposed);
        }
    }
}
