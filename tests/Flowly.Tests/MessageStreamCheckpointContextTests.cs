namespace Flowly.Tests;

public class MessageStreamCheckpointContextTests
{
    public class Equality
    {
        [Fact]
        public void WithSameConsumerNameAndPartition_AreEqual()
        {
            var first = new MessageStreamCheckpointContext("my-handler", 3);
            var second = new MessageStreamCheckpointContext("my-handler", 3);

            Assert.Equal(first, second);
        }

        [Fact]
        public void WithDifferentPartition_AreNotEqual()
        {
            var first = new MessageStreamCheckpointContext("my-handler", 1);
            var second = new MessageStreamCheckpointContext("my-handler", 2);

            Assert.NotEqual(first, second);
        }

        [Fact]
        public void PartitionDefaultsToNullForNonPartitionedStreams()
        {
            var context = new MessageStreamCheckpointContext("my-handler", null);

            Assert.Null(context.Partition);
        }
    }

    public class SaveContext
    {
        [Fact]
        public void CarriesConsumerNamePartitionAndPosition()
        {
            var context = new MessageStreamCheckpointSaveContext("my-handler", 2, 99);

            Assert.Equal("my-handler", context.ConsumerName);
            Assert.Equal(2, context.Partition);
            Assert.Equal(99, context.Position);
        }

        [Fact]
        public void IsAMessageStreamCheckpointContext()
        {
            MessageStreamCheckpointContext context = new MessageStreamCheckpointSaveContext("my-handler", null, 1);

            Assert.Equal("my-handler", context.ConsumerName);
        }
    }
}
