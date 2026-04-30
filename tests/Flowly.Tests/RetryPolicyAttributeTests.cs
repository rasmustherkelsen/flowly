namespace Flowly.Tests;

public class RetryPolicyAttributeTests
{
    public class Constructor
    {
        [Fact]
        public void SetsMaxRetries()
        {
            var retryPolicyAttribute = new RetryPolicyAttribute(5, 30);

            Assert.Equal(5, retryPolicyAttribute.MaxRetries);
        }

        [Fact]
        public void SetsDelaySeconds()
        {
            var retryPolicyAttribute = new RetryPolicyAttribute(5, 30);

            Assert.Equal(30, retryPolicyAttribute.DelaySeconds);
        }

        [Fact]
        public void WithZeroValues_SetsZero()
        {
            var retryPolicyAttribute = new RetryPolicyAttribute(0, 0);

            Assert.Equal(0, retryPolicyAttribute.MaxRetries);
            Assert.Equal(0, retryPolicyAttribute.DelaySeconds);
        }
    }
}