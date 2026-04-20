using Flowly.MessageInfrastructure.Receivers;

namespace Flowly.Tests.MessageInfrastructure.Receivers;

public class ResolvedHandlerQueueOptionsTests
{
    public class Constructor
    {
        [Fact]
        public void StoresAllPropertiesWhenAllProvided()
        {
            var resolvedHandlerQueueOptions = new ResolvedHandlerQueueOptions(
                QueueName: "orders",
                DefaultMessageTimeToLive: TimeSpan.FromHours(1),
                DeadLetterOnMessageExpiration: true,
                LockDuration: TimeSpan.FromMinutes(5),
                MaxRetries: 4,
                RetryDelaySeconds: 20,
                MaxConcurrentCalls: 8);

            Assert.Equal("orders", resolvedHandlerQueueOptions.QueueName);
            Assert.Equal(TimeSpan.FromHours(1), resolvedHandlerQueueOptions.DefaultMessageTimeToLive);
            Assert.True(resolvedHandlerQueueOptions.DeadLetterOnMessageExpiration);
            Assert.Equal(TimeSpan.FromMinutes(5), resolvedHandlerQueueOptions.LockDuration);
            Assert.Equal(4, resolvedHandlerQueueOptions.MaxRetries);
            Assert.Equal(20, resolvedHandlerQueueOptions.RetryDelaySeconds);
            Assert.Equal(8, resolvedHandlerQueueOptions.MaxConcurrentCalls);
        }

        [Fact]
        public void OptionalParameters_HaveDocumentedDefaults()
        {
            var resolvedHandlerQueueOptions = new ResolvedHandlerQueueOptions(
                QueueName: "orders",
                DefaultMessageTimeToLive: TimeSpan.FromHours(1),
                DeadLetterOnMessageExpiration: true,
                LockDuration: TimeSpan.FromMinutes(5));

            Assert.Equal(0, resolvedHandlerQueueOptions.MaxRetries);
            Assert.Equal(0, resolvedHandlerQueueOptions.RetryDelaySeconds);
            Assert.Equal(1, resolvedHandlerQueueOptions.MaxConcurrentCalls);
        }
    }

    public class Equality
    {
        [Fact]
        public void TwoInstancesWithSameValues_AreEqual()
        {
            var first = new ResolvedHandlerQueueOptions("q", TimeSpan.FromHours(1), true, TimeSpan.FromMinutes(5), 3, 10, 4);
            var second = new ResolvedHandlerQueueOptions("q", TimeSpan.FromHours(1), true, TimeSpan.FromMinutes(5), 3, 10, 4);

            Assert.Equal(first, second);
        }

        [Fact]
        public void TwoInstancesWithDifferentValues_AreNotEqual()
        {
            var first = new ResolvedHandlerQueueOptions("a", TimeSpan.FromHours(1), true, TimeSpan.FromMinutes(5));
            var second = new ResolvedHandlerQueueOptions("b", TimeSpan.FromHours(1), true, TimeSpan.FromMinutes(5));

            Assert.NotEqual(first, second);
        }
    }
}
