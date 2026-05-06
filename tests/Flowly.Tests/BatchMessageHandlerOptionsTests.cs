namespace Flowly.Tests;

public class BatchMessageHandlerOptionsTests
{
    public class DefaultValues
    {
        [Fact]
        public void AllPropertiesAreNull()
        {
            var batchMessageHandlerOptions = new BatchMessageHandlerOptions();

            Assert.Null(batchMessageHandlerOptions.MaxMessagesBeforeProcessing);
            Assert.Null(batchMessageHandlerOptions.MaxWaitTime);
            Assert.Null(batchMessageHandlerOptions.QueueName);
            Assert.Null(batchMessageHandlerOptions.DefaultMessageTimeToLive);
            Assert.Null(batchMessageHandlerOptions.DeadLetterOnMessageExpiration);
            Assert.Null(batchMessageHandlerOptions.LockDuration);
            Assert.Null(batchMessageHandlerOptions.MaxConcurrentCalls);
        }
    }

    public class Setters
    {
        [Fact]
        public void StoresBatchProperties()
        {
            var batchMessageHandlerOptions = new BatchMessageHandlerOptions
            {
                MaxMessagesBeforeProcessing = 25,
                MaxWaitTime = TimeSpan.FromSeconds(30)
            };

            Assert.Equal(25, batchMessageHandlerOptions.MaxMessagesBeforeProcessing);
            Assert.Equal(TimeSpan.FromSeconds(30), batchMessageHandlerOptions.MaxWaitTime);
        }

        [Fact]
        public void StoresInheritedQueueProperties()
        {
            var batchMessageHandlerOptions = new BatchMessageHandlerOptions
            {
                QueueName = "my-queue",
                DefaultMessageTimeToLive = TimeSpan.FromDays(2),
                DeadLetterOnMessageExpiration = false,
                LockDuration = TimeSpan.FromMinutes(3),
                MaxConcurrentCalls = 4
            };

            Assert.Equal("my-queue", batchMessageHandlerOptions.QueueName);
            Assert.Equal(TimeSpan.FromDays(2), batchMessageHandlerOptions.DefaultMessageTimeToLive);
            Assert.False(batchMessageHandlerOptions.DeadLetterOnMessageExpiration);
            Assert.Equal(TimeSpan.FromMinutes(3), batchMessageHandlerOptions.LockDuration);
            Assert.Equal(4, batchMessageHandlerOptions.MaxConcurrentCalls);
        }
    }
}
