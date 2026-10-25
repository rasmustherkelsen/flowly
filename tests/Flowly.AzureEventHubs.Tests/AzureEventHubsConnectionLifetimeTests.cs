using Flowly.AzureEventHubs.Tests.Fakes;

namespace Flowly.AzureEventHubs.Tests;

public class AzureEventHubsConnectionLifetimeTests
{
    public class StartAsync
    {
        [Fact]
        public async Task DoesNotDisposeTheClient()
        {
            var clientFactory = new FakeEventHubsClientFactory();
            var azureEventHubsConnectionLifetime = new AzureEventHubsConnectionLifetime(TestAzureEventHubsClient.Create(clientFactory));

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
            var azureEventHubsConnectionLifetime = new AzureEventHubsConnectionLifetime(TestAzureEventHubsClient.Create(clientFactory));

            await azureEventHubsConnectionLifetime.StopAsync(CancellationToken.None);

            Assert.True(clientFactory.Disposed);
        }
    }
}
