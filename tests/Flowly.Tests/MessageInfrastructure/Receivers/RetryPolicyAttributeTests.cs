using Flowly.MessageInfrastructure.Receivers;

namespace Flowly.Tests.MessageInfrastructure.Receivers;

public class RetryPolicyAttributeTests
{
    public class Constructor
    {
        [Fact]
        public void SetsMaxRetries()
        {
            var retryPolicyAttribute = new RetryPolicyAttribute(maxRetries: 5, delaySeconds: 30);

            Assert.Equal(5, retryPolicyAttribute.MaxRetries);
        }

        [Fact]
        public void SetsDelaySeconds()
        {
            var retryPolicyAttribute = new RetryPolicyAttribute(maxRetries: 5, delaySeconds: 30);

            Assert.Equal(30, retryPolicyAttribute.DelaySeconds);
        }

        [Fact]
        public void WithZeroValues_SetsZero()
        {
            var retryPolicyAttribute = new RetryPolicyAttribute(maxRetries: 0, delaySeconds: 0);

            Assert.Equal(0, retryPolicyAttribute.MaxRetries);
            Assert.Equal(0, retryPolicyAttribute.DelaySeconds);
        }
    }
}
