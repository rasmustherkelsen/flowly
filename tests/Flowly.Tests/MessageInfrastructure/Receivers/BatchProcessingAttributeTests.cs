using Flowly.MessageInfrastructure.Receivers;

namespace Flowly.Tests.MessageInfrastructure.Receivers;

public class BatchProcessingAttributeTests
{
    public class Constructor
    {
        [Fact]
        public void SetsMaxMessagesBeforeProcessing()
        {
            var batchProcessingAttribute = new BatchProcessingAttribute(maxMessagesBeforeProcessing: 50, maxWaitTimeInSeconds: 10);

            Assert.Equal(50, batchProcessingAttribute.MaxMessagesBeforeProcessing);
        }

        [Fact]
        public void SetsMaxWaitTimeInSeconds()
        {
            var batchProcessingAttribute = new BatchProcessingAttribute(maxMessagesBeforeProcessing: 50, maxWaitTimeInSeconds: 10);

            Assert.Equal(10, batchProcessingAttribute.MaxWaitTimeInSeconds);
        }
    }
}
