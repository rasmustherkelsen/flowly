using Azure.Messaging.EventHubs.Primitives;
using Azure.Messaging.EventHubs.Processor;
using Flowly.AzureEventHubs.Tests.Fakes;

namespace Flowly.AzureEventHubs.Tests;

public class EventHubsOwnershipOptionsTests
{
    public class Create
    {
        [Fact]
        public void WithoutAnyArgument_Succeeds()
        {
            var eventHubsOwnershipOptions = EventHubsOwnershipOptions.Create(null, null, null);

            Assert.False(eventHubsOwnershipOptions.IsShared);
        }

        [Fact]
        public void WithBothIntervals_KeepsThem()
        {
            var eventHubsOwnershipOptions = EventHubsOwnershipOptions.Create(null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(60));

            Assert.Equal(TimeSpan.FromSeconds(10), eventHubsOwnershipOptions.LoadBalancingInterval);
            Assert.Equal(TimeSpan.FromSeconds(60), eventHubsOwnershipOptions.PartitionOwnershipExpiration);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-5)]
        public void WithLoadBalancingIntervalOfZeroOrLess_Throws(int seconds)
        {
            var exception = Assert.Throws<ArgumentOutOfRangeException>(() => EventHubsOwnershipOptions.Create(null, TimeSpan.FromSeconds(seconds), null));

            Assert.Equal("loadBalancingInterval", exception.ParamName);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-5)]
        public void WithPartitionOwnershipExpirationOfZeroOrLess_Throws(int seconds)
        {
            var exception = Assert.Throws<ArgumentOutOfRangeException>(() => EventHubsOwnershipOptions.Create(null, null, TimeSpan.FromSeconds(seconds)));

            Assert.Equal("partitionOwnershipExpiration", exception.ParamName);
        }

        [Fact]
        public void WithExpirationNotGreaterThanTheInterval_Throws()
        {
            var exception = Assert.Throws<ArgumentException>(() => EventHubsOwnershipOptions.Create(null, TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(60)));

            Assert.Equal("partitionOwnershipExpiration", exception.ParamName);
        }

        [Fact]
        public void WithOnlyTheIntervalPastTheDefaultExpiration_Throws()
        {
            var exception = Assert.Throws<ArgumentException>(() => EventHubsOwnershipOptions.Create(null, TimeSpan.FromMinutes(3), null));

            Assert.Contains("set both parameters", exception.Message);
        }

        [Fact]
        public void WithOnlyTheExpirationBelowTheDefaultInterval_Throws()
        {
            Assert.Throws<ArgumentException>(() => EventHubsOwnershipOptions.Create(null, null, TimeSpan.FromSeconds(10)));
        }

        [Fact]
        public void WithAContainer_IsShared()
        {
            Assert.True(EventHubsOwnershipOptions.Create(new FakeBlobContainerClient(true), null, null).IsShared);
        }
    }

    public class ApplyTo
    {
        [Fact]
        public void WithoutIntervals_KeepsTheSdkDefaults()
        {
            var defaults = new EventProcessorOptions();

            var processorOptions = EventHubsOwnershipOptions.Create(null, null, null).ApplyTo(new EventProcessorOptions());

            Assert.Equal(defaults.LoadBalancingUpdateInterval, processorOptions.LoadBalancingUpdateInterval);
            Assert.Equal(defaults.PartitionOwnershipExpirationInterval, processorOptions.PartitionOwnershipExpirationInterval);
        }

        [Fact]
        public void WithIntervals_SetsThemOnTheProcessorOptions()
        {
            var processorOptions = EventHubsOwnershipOptions.Create(null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(40)).ApplyTo(new EventProcessorOptions());

            Assert.Equal(TimeSpan.FromSeconds(5), processorOptions.LoadBalancingUpdateInterval);
            Assert.Equal(TimeSpan.FromSeconds(40), processorOptions.PartitionOwnershipExpirationInterval);
        }
    }

    public class CreateStore
    {
        [Fact]
        public void WithoutContainer_IsAnInMemoryStore()
        {
            Assert.IsType<InMemoryPartitionOwnershipStore>(EventHubsOwnershipOptions.Create(null, null, null).CreateStore());
        }

        [Fact]
        public void WithContainer_IsTheStockBlobCheckpointStore()
        {
            Assert.IsType<BlobCheckpointStore>(EventHubsOwnershipOptions.Create(UnconnectedBlobContainerClient.Create(), null, null).CreateStore());
        }

        [Fact]
        public void WithoutContainer_CreatesAnIndependentStoreEachTime()
        {
            var eventHubsOwnershipOptions = EventHubsOwnershipOptions.Create(null, null, null);

            Assert.NotSame(eventHubsOwnershipOptions.CreateStore(), eventHubsOwnershipOptions.CreateStore());
        }
    }
}
