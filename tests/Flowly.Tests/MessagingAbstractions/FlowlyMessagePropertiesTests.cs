using Flowly.Transport;

namespace Flowly.Tests.MessagingAbstractions;

public class FlowlyMessagePropertiesTests
{
    public class Constants
    {
        [Fact]
        public void RetryCountIsFlowlyRetryCount()
        {
            Assert.Equal("flowly-retry-count", FlowlyMessageProperties.RetryCount);
        }

        [Fact]
        public void TargetSubscriptionIsFlowlyTargetSubscription()
        {
            Assert.Equal("flowly-target-subscription", FlowlyMessageProperties.TargetSubscription);
        }

        [Fact]
        public void ScheduledEnqueueTimeIsFlowlyScheduledEnqueueTime()
        {
            Assert.Equal("flowly-scheduled-enqueue-time", FlowlyMessageProperties.ScheduledEnqueueTime);
        }
    }
}