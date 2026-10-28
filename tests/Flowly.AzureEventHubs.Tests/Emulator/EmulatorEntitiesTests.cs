using System.Text.Json.Nodes;

namespace Flowly.AzureEventHubs.Tests.Emulator;

public class EmulatorEntitiesTests
{
    public class ToConfiguration
    {
        [Fact]
        public void MatchesTheEmulatorConfigurationFileShippedWithTheTests()
        {
            var shipped = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Emulator", "Config.json")));

            Assert.True(JsonNode.DeepEquals(EmulatorEntities.ToConfiguration(), shipped), "Emulator/Config.json differs from EmulatorEntities; regenerate it from the declared hubs.");
        }

        [Fact]
        public void StaysWithinTheEmulatorQuotas()
        {
            Assert.True(EmulatorEntities.Hubs.Count <= 10);
            Assert.All(EmulatorEntities.Hubs, hub => Assert.InRange(hub.ConsumerGroupCount, 1, 20));
            Assert.All(EmulatorEntities.Hubs, hub => Assert.InRange(hub.PartitionCount, 1, 32));
        }
    }

    public class Next
    {
        [Fact]
        public void ReturnsEachDeclaredConsumerGroupInTurnAndStartsOverAfterTheLast()
        {
            var consumerGroupAllocator = new ConsumerGroupAllocator([new EmulatorHub("hub", 1, 2)]);

            var groups = Enumerable.Range(0, 3).Select(_ => consumerGroupAllocator.Next("hub")).ToList();

            Assert.Equal(["cg01", "cg02", "cg01"], groups);
        }
    }
}
