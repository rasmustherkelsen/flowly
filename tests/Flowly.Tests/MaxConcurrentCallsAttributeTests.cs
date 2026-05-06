namespace Flowly.Tests;

public class MaxConcurrentCallsAttributeTests
{
    public class Constructor
    {
        [Fact]
        public void SetsMaxConcurrentCalls()
        {
            var maxConcurrentCallsAttribute = new MaxConcurrentCallsAttribute(8);

            Assert.Equal(8, maxConcurrentCallsAttribute.MaxConcurrentCalls);
        }

        [Fact]
        public void WithZero_SetsZero()
        {
            var maxConcurrentCallsAttribute = new MaxConcurrentCallsAttribute(0);

            Assert.Equal(0, maxConcurrentCallsAttribute.MaxConcurrentCalls);
        }
    }
}