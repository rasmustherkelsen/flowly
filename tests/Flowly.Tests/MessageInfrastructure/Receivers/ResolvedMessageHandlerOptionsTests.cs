using Flowly.MessageInfrastructure.Receivers;

namespace Flowly.Tests.MessageInfrastructure.Receivers;

public class ResolvedMessageHandlerOptionsTests
{
    public class Constructor
    {
        [Fact]
        public void StoresAllProperties()
        {
            var resolved = new ResolvedMessageHandlerOptions(
                QueueName: "my-queue",
                DefaultMessageTimeToLive: TimeSpan.FromDays(2),
                DeadLetterOnMessageExpiration: false,
                LockDuration: TimeSpan.FromMinutes(3),
                MaxRetries: 4,
                RetryDelaySeconds: 20,
                MaxConcurrentCalls: 8,
                MaxMessagesBeforeProcessing: 50,
                MaxWaitTime: TimeSpan.FromSeconds(10));

            Assert.Equal("my-queue", resolved.QueueName);
            Assert.Equal(TimeSpan.FromDays(2), resolved.DefaultMessageTimeToLive);
            Assert.False(resolved.DeadLetterOnMessageExpiration);
            Assert.Equal(TimeSpan.FromMinutes(3), resolved.LockDuration);
            Assert.Equal(4, resolved.MaxRetries);
            Assert.Equal(20, resolved.RetryDelaySeconds);
            Assert.Equal(8, resolved.MaxConcurrentCalls);
            Assert.Equal(50, resolved.MaxMessagesBeforeProcessing);
            Assert.Equal(TimeSpan.FromSeconds(10), resolved.MaxWaitTime);
        }
    }

    public class Equality
    {
        [Fact]
        public void TwoInstancesWithSameValues_AreEqual()
        {
            var first = new ResolvedMessageHandlerOptions("q", TimeSpan.FromDays(1), true, TimeSpan.FromMinutes(5), 3, 10, 4, 100, TimeSpan.FromSeconds(30));
            var second = new ResolvedMessageHandlerOptions("q", TimeSpan.FromDays(1), true, TimeSpan.FromMinutes(5), 3, 10, 4, 100, TimeSpan.FromSeconds(30));

            Assert.Equal(first, second);
        }

        [Fact]
        public void TwoInstancesWithDifferentValues_AreNotEqual()
        {
            var first = new ResolvedMessageHandlerOptions("a", TimeSpan.FromDays(1), true, TimeSpan.FromMinutes(5), 0, 0, 1, 0, default);
            var second = new ResolvedMessageHandlerOptions("b", TimeSpan.FromDays(1), true, TimeSpan.FromMinutes(5), 0, 0, 1, 0, default);

            Assert.NotEqual(first, second);
        }
    }
}
