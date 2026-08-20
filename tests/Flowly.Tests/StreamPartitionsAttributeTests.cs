namespace Flowly.Tests;

public class StreamPartitionsAttributeTests
{
    public class Constructor
    {
        [Fact]
        public void ExposesCount()
        {
            var attribute = new StreamPartitionsAttribute(4);

            Assert.Equal(4, attribute.Count);
        }

        [Fact]
        public void IsReadableFromDecoratedMessageContract()
        {
            var attribute = (StreamPartitionsAttribute?)Attribute.GetCustomAttribute(typeof(PartitionedMessage), typeof(StreamPartitionsAttribute));

            Assert.NotNull(attribute);
            Assert.Equal(8, attribute.Count);
        }

        [Fact]
        public void WithZeroCount_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new StreamPartitionsAttribute(0));
        }

        [Fact]
        public void WithNegativeCount_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new StreamPartitionsAttribute(-1));
        }
    }

    [StreamPartitions(8)]
    private record PartitionedMessage;
}
