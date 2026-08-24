using Flowly.MessageInfrastructure.Registration;

namespace Flowly.Tests.MessageInfrastructure.Registration;

public class PartitionRoundRobinTests
{
    public class Next
    {
        [Fact]
        public void DistributesSequentiallyAcrossPartitions()
        {
            var partitionRoundRobin = new PartitionRoundRobin();

            var partitions = Enumerable.Range(0, 8).Select(_ => partitionRoundRobin.Next(4)).ToList();

            Assert.All(partitions, partition => Assert.InRange(partition, 0, 3));
            Assert.Equal(2, partitions.Count(p => p == 0));
            Assert.Equal(2, partitions.Count(p => p == 1));
            Assert.Equal(2, partitions.Count(p => p == 2));
            Assert.Equal(2, partitions.Count(p => p == 3));
        }

        [Fact]
        public void AtCounterWraparound_StaysWithinPartitionRange()
        {
            var partitionRoundRobin = new PartitionRoundRobin(ulong.MaxValue - 2);

            var partitions = Enumerable.Range(0, 5).Select(_ => partitionRoundRobin.Next(4)).ToList();

            Assert.All(partitions, partition => Assert.InRange(partition, 0, 3));
        }
    }
}
