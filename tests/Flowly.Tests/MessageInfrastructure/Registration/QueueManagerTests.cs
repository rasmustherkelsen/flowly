using Flowly.MessageInfrastructure.Registration;

namespace Flowly.Tests.MessageInfrastructure.Registration;

public class QueueManagerTests
{
    public class RegisterQueue
    {
        [Fact]
        public void WithSingleRegistration_StoresQueue()
        {
            var queueManager = new QueueManager();

            queueManager.RegisterQueue(new DeferredQueueRegistration("order-placed"));

            var queues = queueManager.GetRegisteredQueues();
            Assert.Single(queues);
            Assert.Equal("order-placed", queues[0].Name);
        }

        [Fact]
        public void WithWhitespaceQueueName_IgnoresRegistration()
        {
            var queueManager = new QueueManager();

            queueManager.RegisterQueue(new DeferredQueueRegistration("   "));

            Assert.Empty(queueManager.GetRegisteredQueues());
        }

        [Fact]
        public void WithEmptyQueueName_IgnoresRegistration()
        {
            var queueManager = new QueueManager();

            queueManager.RegisterQueue(new DeferredQueueRegistration(""));

            Assert.Empty(queueManager.GetRegisteredQueues());
        }

        [Fact]
        public void WithDuplicateQueueNameAndNoConflict_MergesSingleEntry()
        {
            var queueManager = new QueueManager();

            queueManager.RegisterQueue(new DeferredQueueRegistration("order-placed", DefaultMessageTimeToLive: TimeSpan.FromDays(2)));
            queueManager.RegisterQueue(new DeferredQueueRegistration("order-placed", LockDuration: TimeSpan.FromMinutes(10)));

            var queues = queueManager.GetRegisteredQueues();
            Assert.Single(queues);
            Assert.Equal(TimeSpan.FromDays(2), queues[0].DefaultMessageTimeToLive);
            Assert.Equal(TimeSpan.FromMinutes(10), queues[0].LockDuration);
        }

        [Fact]
        public void WithDuplicateQueueNameAndIdenticalSettings_MergesSingleEntry()
        {
            var queueManager = new QueueManager();
            var ttl = TimeSpan.FromHours(4);

            queueManager.RegisterQueue(new DeferredQueueRegistration("order-placed", DefaultMessageTimeToLive: ttl));
            queueManager.RegisterQueue(new DeferredQueueRegistration("order-placed", DefaultMessageTimeToLive: ttl));

            var queues = queueManager.GetRegisteredQueues();
            Assert.Single(queues);
            Assert.Equal(ttl, queues[0].DefaultMessageTimeToLive);
        }

        [Fact]
        public void WithConflictingTimeToLive_ThrowsInvalidOperationException()
        {
            var queueManager = new QueueManager();
            queueManager.RegisterQueue(new DeferredQueueRegistration("order-placed", DefaultMessageTimeToLive: TimeSpan.FromDays(1)));

            var exception = Assert.Throws<InvalidOperationException>(() =>
                queueManager.RegisterQueue(new DeferredQueueRegistration("order-placed", DefaultMessageTimeToLive: TimeSpan.FromDays(7))));

            Assert.Contains("DefaultMessageTimeToLive", exception.Message);
            Assert.Contains("order-placed", exception.Message);
        }

        [Fact]
        public void WithConflictingLockDuration_ThrowsInvalidOperationException()
        {
            var queueManager = new QueueManager();
            queueManager.RegisterQueue(new DeferredQueueRegistration("order-placed", LockDuration: TimeSpan.FromMinutes(5)));

            var exception = Assert.Throws<InvalidOperationException>(() =>
                queueManager.RegisterQueue(new DeferredQueueRegistration("order-placed", LockDuration: TimeSpan.FromMinutes(10))));

            Assert.Contains("LockDuration", exception.Message);
        }

        [Fact]
        public void WithConflictingDeadLetterOnMessageExpiration_ThrowsInvalidOperationException()
        {
            var queueManager = new QueueManager();
            queueManager.RegisterQueue(new DeferredQueueRegistration("order-placed", DeadLetterOnMessageExpiration: true));

            var exception = Assert.Throws<InvalidOperationException>(() =>
                queueManager.RegisterQueue(new DeferredQueueRegistration("order-placed", DeadLetterOnMessageExpiration: false)));

            Assert.Contains("DeadLetterOnMessageExpiration", exception.Message);
        }

        [Fact]
        public void WithEitherRegistrationRequiringSession_SetsRequiresSessionTrue()
        {
            var queueManager = new QueueManager();

            queueManager.RegisterQueue(new DeferredQueueRegistration("order-placed", RequiresSession: false));
            queueManager.RegisterQueue(new DeferredQueueRegistration("order-placed", RequiresSession: true));

            var queues = queueManager.GetRegisteredQueues();
            Assert.Single(queues);
            Assert.True(queues[0].RequiresSession);
        }

        [Fact]
        public void WithDifferentCasedQueueName_TreatsAsSameQueue()
        {
            var queueManager = new QueueManager();

            queueManager.RegisterQueue(new DeferredQueueRegistration("Order-Placed"));
            queueManager.RegisterQueue(new DeferredQueueRegistration("order-placed"));

            Assert.Single(queueManager.GetRegisteredQueues());
        }
    }

    public class GetRegisteredQueues
    {
        [Fact]
        public void WithoutAnyRegistrations_ReturnsEmptyList()
        {
            var queueManager = new QueueManager();

            var queues = queueManager.GetRegisteredQueues();

            Assert.Empty(queues);
        }

        [Fact]
        public void WithMultipleRegistrations_ReturnsAllQueuesOrderedByName()
        {
            var queueManager = new QueueManager();
            queueManager.RegisterQueue(new DeferredQueueRegistration("invoice-created"));
            queueManager.RegisterQueue(new DeferredQueueRegistration("order-placed"));
            queueManager.RegisterQueue(new DeferredQueueRegistration("account-updated"));

            var queues = queueManager.GetRegisteredQueues();

            Assert.Equal(3, queues.Count);
            Assert.Equal("account-updated", queues[0].Name);
            Assert.Equal("invoice-created", queues[1].Name);
            Assert.Equal("order-placed", queues[2].Name);
        }

        [Fact]
        public void WithoutSettings_UsesFrameworkDefaults()
        {
            var queueManager = new QueueManager();
            queueManager.RegisterQueue(new DeferredQueueRegistration("order-placed"));

            var queues = queueManager.GetRegisteredQueues();

            Assert.Equal(TimeSpan.FromDays(1), queues[0].DefaultMessageTimeToLive);
            Assert.True(queues[0].DeadLetterOnMessageExpiration);
            Assert.Equal(TimeSpan.FromMinutes(5), queues[0].LockDuration);
            Assert.False(queues[0].RequiresSession);
        }

        [Fact]
        public void WithExplicitSettings_PropagatesToDescription()
        {
            var queueManager = new QueueManager();
            queueManager.RegisterQueue(new DeferredQueueRegistration(
                "order-placed",
                RequiresSession: true,
                DefaultMessageTimeToLive: TimeSpan.FromHours(3),
                DeadLetterOnMessageExpiration: false,
                LockDuration: TimeSpan.FromSeconds(45)));

            var queues = queueManager.GetRegisteredQueues();

            Assert.Equal(TimeSpan.FromHours(3), queues[0].DefaultMessageTimeToLive);
            Assert.False(queues[0].DeadLetterOnMessageExpiration);
            Assert.Equal(TimeSpan.FromSeconds(45), queues[0].LockDuration);
            Assert.True(queues[0].RequiresSession);
        }

        [Fact]
        public void CalledMultipleTimes_ReturnsCachedResult()
        {
            var queueManager = new QueueManager();
            queueManager.RegisterQueue(new DeferredQueueRegistration("order-placed"));

            var first = queueManager.GetRegisteredQueues();
            var second = queueManager.GetRegisteredQueues();

            Assert.Same(first, second);
        }
    }
}
