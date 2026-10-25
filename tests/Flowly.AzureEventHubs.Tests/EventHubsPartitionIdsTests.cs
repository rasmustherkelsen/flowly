namespace Flowly.AzureEventHubs.Tests;

public class EventHubsPartitionIdsTests
{
    public class Expected
    {
        [Fact]
        public void WithDeclaredCount_ReturnsIdsFromZeroUpToCountMinusOne()
        {
            Assert.Equal(["0", "1", "2"], EventHubsPartitionIds.Expected(3));
        }

        [Fact]
        public void WithoutDeclaredCount_ReturnsOnlyZero()
        {
            Assert.Equal(["0"], EventHubsPartitionIds.Expected(null));
        }
    }

    public class Matches
    {
        [Fact]
        public void WithExactlyTheDeclaredIdsInAnyOrder_ReturnsTrue()
        {
            Assert.True(EventHubsPartitionIds.Matches(["2", "0", "1"], 3));
        }

        [Fact]
        public void WithFewerIdsThanDeclared_ReturnsFalse()
        {
            Assert.False(EventHubsPartitionIds.Matches(["0", "1"], 3));
        }

        [Fact]
        public void WithMoreIdsThanDeclared_ReturnsFalse()
        {
            Assert.False(EventHubsPartitionIds.Matches(["0", "1", "2", "3"], 3));
        }

        [Fact]
        public void WithNonNumericIds_ReturnsFalse()
        {
            Assert.False(EventHubsPartitionIds.Matches(["a", "b"], 2));
        }

        [Fact]
        public void WithZeroPaddedIds_ReturnsFalse()
        {
            Assert.False(EventHubsPartitionIds.Matches(["00", "01"], 2));
        }

        [Fact]
        public void WithIdsNotStartingAtZero_ReturnsFalse()
        {
            Assert.False(EventHubsPartitionIds.Matches(["1", "2"], 2));
        }

        [Fact]
        public void WithoutDeclaredCountAndSingleZeroId_ReturnsTrue()
        {
            Assert.True(EventHubsPartitionIds.Matches(["0"], null));
        }

        [Fact]
        public void WithoutDeclaredCountAndSeveralIds_ReturnsFalse()
        {
            Assert.False(EventHubsPartitionIds.Matches(["0", "1"], null));
        }

        [Fact]
        public void WithDuplicateIds_ReturnsFalse()
        {
            Assert.False(EventHubsPartitionIds.Matches(["0", "0"], 2));
        }
    }

    public class ToPartition
    {
        [Theory]
        [InlineData("0", 0)]
        [InlineData("7", 7)]
        [InlineData("31", 31)]
        public void WithCanonicalNumber_ReturnsThePartition(string partitionId, int expected)
        {
            Assert.Equal(expected, EventHubsPartitionIds.ToPartition(partitionId));
        }

        [Theory]
        [InlineData("-1")]
        [InlineData("01")]
        [InlineData("+1")]
        [InlineData(" 1")]
        [InlineData("1.0")]
        [InlineData("a")]
        [InlineData("")]
        [InlineData("99999999999")]
        public void WithAnythingElse_ReturnsNull(string partitionId)
        {
            Assert.Null(EventHubsPartitionIds.ToPartition(partitionId));
        }
    }

    public class ToPartitionId
    {
        [Fact]
        public void IsTheInverseOfToPartition()
        {
            Assert.Equal(12, EventHubsPartitionIds.ToPartition(EventHubsPartitionIds.ToPartitionId(12)));
        }
    }
}
