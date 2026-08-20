using Flowly.MessageInfrastructure.Registration;

namespace Flowly.Tests.MessageInfrastructure.Registration;

public class PartitionKeyHasherTests
{
    public class Resolve
    {
        [Fact]
        public void WithSameKey_AlwaysReturnsSamePartition()
        {
            var first = PartitionKeyHasher.Resolve("customer-1", 8);
            var second = PartitionKeyHasher.Resolve("customer-1", 8);

            Assert.Equal(first, second);
        }

        [Theory]
        [InlineData("customer-1", 8)]
        [InlineData("order-42", 3)]
        [InlineData("", 4)]
        public void ReturnsIndexWithinPartitionRange(string partitionKey, int partitionCount)
        {
            var partition = PartitionKeyHasher.Resolve(partitionKey, partitionCount);

            Assert.InRange(partition, 0, partitionCount - 1);
        }

        [Fact]
        public void WithDifferentKeys_CanReturnDifferentPartitions()
        {
            var partitions = Enumerable.Range(0, 100)
                .Select(i => PartitionKeyHasher.Resolve($"key-{i}", 8))
                .Distinct()
                .ToList();

            Assert.True(partitions.Count > 1);
        }
    }
}
