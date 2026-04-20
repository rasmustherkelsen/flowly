using Flowly.MessageInfrastructure.Receivers;

namespace Flowly.Tests.MessageInfrastructure.Receivers;

public class HandlerQueueOptionsTests
{
    public class DefaultValues
    {
        [Fact]
        public void AllPropertiesAreNull()
        {
            var handlerQueueOptions = new HandlerQueueOptions();

            Assert.Null(handlerQueueOptions.QueueName);
            Assert.Null(handlerQueueOptions.DefaultMessageTimeToLive);
            Assert.Null(handlerQueueOptions.DeadLetterOnMessageExpiration);
            Assert.Null(handlerQueueOptions.LockDuration);
            Assert.Null(handlerQueueOptions.MaxRetries);
            Assert.Null(handlerQueueOptions.RetryDelaySeconds);
            Assert.Null(handlerQueueOptions.MaxConcurrentCalls);
        }
    }

    public class Setters
    {
        [Fact]
        public void StoresAllValues()
        {
            var handlerQueueOptions = new HandlerQueueOptions
            {
                QueueName = "orders",
                DefaultMessageTimeToLive = TimeSpan.FromHours(1),
                DeadLetterOnMessageExpiration = true,
                LockDuration = TimeSpan.FromMinutes(5),
                MaxRetries = 3,
                RetryDelaySeconds = 10,
                MaxConcurrentCalls = 4
            };

            Assert.Equal("orders", handlerQueueOptions.QueueName);
            Assert.Equal(TimeSpan.FromHours(1), handlerQueueOptions.DefaultMessageTimeToLive);
            Assert.True(handlerQueueOptions.DeadLetterOnMessageExpiration);
            Assert.Equal(TimeSpan.FromMinutes(5), handlerQueueOptions.LockDuration);
            Assert.Equal(3, handlerQueueOptions.MaxRetries);
            Assert.Equal(10, handlerQueueOptions.RetryDelaySeconds);
            Assert.Equal(4, handlerQueueOptions.MaxConcurrentCalls);
        }
    }
}
