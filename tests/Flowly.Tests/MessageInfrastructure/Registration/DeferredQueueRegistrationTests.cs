using Flowly.MessageInfrastructure.Registration;

namespace Flowly.Tests.MessageInfrastructure.Registration;

public class DeferredQueueRegistrationTests
{
    public class Constructor
    {
        [Fact]
        public void WithRequiredQueueName_SetsQueueNameAndLeavesOtherValuesDefault()
        {
            var deferredQueueRegistration = new DeferredQueueRegistration("orders");

            Assert.Equal("orders", deferredQueueRegistration.QueueName);
            Assert.False(deferredQueueRegistration.RequiresSession);
            Assert.Null(deferredQueueRegistration.DefaultMessageTimeToLive);
            Assert.Null(deferredQueueRegistration.DeadLetterOnMessageExpiration);
            Assert.Null(deferredQueueRegistration.LockDuration);
        }

        [Fact]
        public void WithAllArguments_StoresEachValue()
        {
            var deferredQueueRegistration = new DeferredQueueRegistration(
                QueueName: "orders",
                RequiresSession: true,
                DefaultMessageTimeToLive: TimeSpan.FromHours(2),
                DeadLetterOnMessageExpiration: true,
                LockDuration: TimeSpan.FromMinutes(5));

            Assert.Equal("orders", deferredQueueRegistration.QueueName);
            Assert.True(deferredQueueRegistration.RequiresSession);
            Assert.Equal(TimeSpan.FromHours(2), deferredQueueRegistration.DefaultMessageTimeToLive);
            Assert.True(deferredQueueRegistration.DeadLetterOnMessageExpiration);
            Assert.Equal(TimeSpan.FromMinutes(5), deferredQueueRegistration.LockDuration);
        }
    }

    public class Equality
    {
        [Fact]
        public void TwoInstancesWithSameValues_AreEqual()
        {
            var first = new DeferredQueueRegistration("orders", true, TimeSpan.FromHours(1), false, TimeSpan.FromMinutes(5));
            var second = new DeferredQueueRegistration("orders", true, TimeSpan.FromHours(1), false, TimeSpan.FromMinutes(5));

            Assert.Equal(first, second);
        }
    }
}
