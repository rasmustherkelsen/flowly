namespace Flowly.Tests;

public class QueueNameAttributeTests
{
    public class Constructor
    {
        [Fact]
        public void SetsQueueName()
        {
            var queueNameAttribute = new QueueNameAttribute("my-queue");

            Assert.Equal("my-queue", queueNameAttribute.QueueName);
        }
    }
}