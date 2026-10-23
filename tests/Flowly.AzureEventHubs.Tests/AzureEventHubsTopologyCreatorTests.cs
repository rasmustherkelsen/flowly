namespace Flowly.AzureEventHubs.Tests;

public class AzureEventHubsTopologyCreatorTests
{
    public class CreateTopology
    {
        [Fact]
        public async Task CompletesWithoutCreatingAnything()
        {
            var azureEventHubsTopologyCreator = new AzureEventHubsTopologyCreator();

            await azureEventHubsTopologyCreator.CreateTopology([], CancellationToken.None);
        }
    }
}
